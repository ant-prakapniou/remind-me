using RemindMe.Core;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace RemindMe.Bot;

/// <summary>
/// Runs the reminder-delivery loop inside the bot process.
/// </summary>
/// <remarks>
/// <para>
/// On Railway, volumes cannot be shared between services, so the cron job
/// cannot run as a separate service and still see the same <c>notes.json</c>
/// as the bot. Instead, the bot hosts the delivery loop itself: a
/// <see cref="PeriodicTimer"/> inside <c>Program.cs</c> calls
/// <see cref="RunOnceAsync"/> once per minute.
/// </para>
/// <para>
/// Unlike the previous standalone cron process, this runner never calls
/// <see cref="Environment.Exit(int)"/>. The bot process must stay alive.
/// All exceptions are caught and logged so a failed delivery attempt cannot
/// take down the bot.
/// </para>
/// </remarks>
public sealed class CronRunner
{
    private readonly NoteStore _store;
    private readonly LocalTime _localTime;
    private readonly TelegramBotClient _bot;

    /// <summary>
    /// Initializes a new instance of the <see cref="CronRunner"/> class.
    /// </summary>
    /// <param name="store">The store used to read and update notes.</param>
    /// <param name="localTime">Converter between UTC and the user's local time.</param>
    /// <param name="bot">Telegram client used to deliver reminders.</param>
    public CronRunner(NoteStore store, LocalTime localTime, TelegramBotClient bot)
    {
        _store = store;
        _localTime = localTime;
        _bot = bot;
    }

    /// <summary>
    /// Sends all reminders whose time has come and removes them from storage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads all notes, selects those with <see cref="Note.RemindAtUtc"/> less
    /// than or equal to <see cref="DateTime.UtcNow"/>, sends each one, and
    /// removes successfully sent notes from the store. Notes that fail to send
    /// (e.g. blocked bot) are kept and retried on the next tick.
    /// </para>
    /// <para>
    /// Transient Telegram errors (5xx, 429, network timeouts) are retried
    /// in-place via <see cref="TelegramApiRetry"/>, so a brief Telegram outage
    /// does not delay a reminder by a full tick.
    /// </para>
    /// <para>
    /// If there are no due reminders, logs <c>[INFO] No due reminders.</c>
    /// and returns immediately.
    /// </para>
    /// </remarks>
    public async Task RunOnceAsync()
    {
        try
        {
            var notes = _store.Load();
            var now = DateTime.UtcNow;
            var due = notes.Where(n => n.RemindAtUtc <= now).ToList();

            if (due.Count == 0)
            {
                Console.WriteLine("[INFO] No due reminders.");
                return;
            }

            var sentIds = new HashSet<string>();

            foreach (var note in due)
            {
                var when = _localTime.ToLocal(note.RemindAtUtc).ToString("yyyy-MM-dd HH:mm");
                var text = Messages.Reminder(note.Text, when);

                try
                {
                    // Retries transient errors (5xx/429/timeouts) in-place.
                    // Throws only after maxAttempts are exhausted.
                    await TelegramApiRetry.ExecuteAsync(
                        "SendMessage(reminder)",
                        ct => _bot.SendMessage(chatId: note.OwnerUserId, text: text, cancellationToken: ct),
                        maxAttempts: 5,
                        CancellationToken.None);

                    sentIds.Add(note.Id);
                    Console.WriteLine($"[INFO] Sent note {note.Id} to {note.OwnerUserId}");
                }
                catch (ApiRequestException ex) when (ex.ErrorCode == 429)
                {
                    // Telegram rate-limited us even after retries. Keep the note;
                    // the next tick will try again.
                    Console.WriteLine($"[WARN] Rate limited on note {note.Id}, will retry next run: {ex.Message}");
                }
                catch (Exception ex)
                {
                    // Non-transient (e.g. blocked bot) or retries exhausted.
                    // Keep the note so it is retried on the next tick.
                    Console.WriteLine($"[WARN] Failed to send note {note.Id}: {ex.Message}");
                }
            }

            if (sentIds.Count > 0)
            {
                notes.RemoveAll(n => sentIds.Contains(n.Id));
                _store.Save(notes);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Cron run failed: {ex.Message}");
        }
    }
}