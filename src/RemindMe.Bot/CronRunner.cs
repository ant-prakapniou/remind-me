using RemindMe.Core;
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
/// <para>
/// The primary constructor takes the note store, the local-time converter,
/// the Telegram sender, and an optional retry limit
/// (<c>maxSendAttempts</c>, default 5). The limit exists so tests can set
/// it to 1 and avoid waiting through the real retry backoff.
/// </para>
/// </remarks>
public sealed class CronRunner(NoteStore store, LocalTime localTime, ITelegramSender sender, int maxSendAttempts = 5)
{
    private readonly NoteStore _store = store;
    private readonly LocalTime _localTime = localTime;
    private readonly ITelegramSender _sender = sender;
    private readonly int _maxSendAttempts = maxSendAttempts;

    /// <summary>
    /// Sends all reminders whose time has come and removes them from storage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Delivery is two-phase:
    /// <list type="number">
    ///   <item><description>
    ///     <b>Read phase.</b> Under the store's lock, load the current notes
    ///     and select those with <see cref="Note.RemindAtUtc"/> less than or
    ///     equal to <see cref="DateTime.UtcNow"/>. The selected notes are
    ///     <i>not</i> removed yet.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Send phase.</b> Outside the lock, send each due note via Telegram.
    ///     This may take seconds (network, retries) and must not block other
    ///     writers of the store (the bot creating new notes).
    ///   </description></item>
    ///   <item><description>
    ///     <b>Remove phase.</b> Under the lock again, remove from storage the
    ///     notes that were successfully sent, and also the notes that Telegram
    ///     permanently rejected (see below).
    ///   </description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Delivery outcomes:
    /// <list type="bullet">
    ///   <item><description>
    ///     <b>Sent.</b> Removed from storage.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Permanently rejected</b> — <see cref="ApiRequestException"/>
    ///     with <c>ErrorCode</c> 400 (chat not found, message too long, etc.)
    ///     or 403 (bot blocked by the user, bot kicked from the chat).
    ///     Retrying cannot succeed, so the note is dropped and an
    ///     <c>[INFO] Dropped note ...</c> line is logged.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Transient failure</b> — 429, 5xx after retries, network errors.
    ///     The note is kept and retried on the next tick.
    ///   </description></item>
    /// </list>
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
            // Phase 1: read due notes under the lock. No I/O with Telegram here.
            List<Note> due = [];
            _store.Update(notes =>
            {
                var now = DateTime.UtcNow;
                due = notes.Where(n => n.RemindAtUtc <= now).ToList();
            });

            if (due.Count == 0)
            {
                Console.WriteLine("[INFO] No due reminders.");
                return;
            }

            // Phase 2: send outside the lock. Telegram calls can take seconds.
            var sentIds = new HashSet<string>();
            var deadIds = new HashSet<string>();

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
                        ct => _sender.SendMessageAsync(note.OwnerUserId, text, ct),
                        maxAttempts: _maxSendAttempts,
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
                catch (ApiRequestException ex) when (IsPermanentDeliveryFailure(ex.ErrorCode))
                {
                    // Permanent rejection: chat does not exist, bot was blocked
                    // or kicked, message too long, etc. Retrying cannot succeed,
                    // so drop the note instead of keeping it forever.
                    deadIds.Add(note.Id);
                    Console.WriteLine($"[INFO] Dropped note {note.Id} (chat {note.OwnerUserId}, error {ex.ErrorCode}): {ex.Message}");
                }
                catch (Exception ex)
                {
                    // Non-transient (unknown) or retries exhausted on a
                    // possibly-transient error. Keep the note and retry next tick.
                    Console.WriteLine($"[WARN] Failed to send note {note.Id}: {ex.Message}");
                }
            }

            // Phase 3: remove sent and dead notes under the lock.
            if (sentIds.Count > 0 || deadIds.Count > 0)
            {
                _store.Update(notes =>
                {
                    notes.RemoveAll(n => sentIds.Contains(n.Id) || deadIds.Contains(n.Id));
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Cron run failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns <c>true</c> if the Telegram error code denotes a permanent
    /// delivery failure that should not be retried.
    /// </summary>
    /// <param name="errorCode">The <see cref="ApiRequestException.ErrorCode"/> from Telegram.</param>
    /// <returns>
    /// <c>true</c> for 400 (bad request: chat not found, message too long, etc.)
    /// and 403 (forbidden: bot blocked or kicked); otherwise <c>false</c>.
    /// </returns>
    /// <remarks>
    /// Kept as a separate internal method so the classification is unit-testable
    /// without constructing an <see cref="ApiRequestException"/>.
    /// </remarks>
    internal static bool IsPermanentDeliveryFailure(int errorCode) =>
        errorCode is 400 or 403;
}