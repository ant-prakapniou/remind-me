using Microsoft.Extensions.Configuration;
using RemindMe.Core;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var botToken = config["Telegram:BotToken"]
    ?? throw new InvalidOperationException("Telegram:BotToken is not configured");
var notesPath = config["Storage:NotesFilePath"]
    ?? throw new InvalidOperationException("Storage:NotesFilePath is not configured");
var offsetHours = config.GetValue<int?>("LocalTime:OffsetHours") ?? 3;

var store = new NoteStore(notesPath);
var localTime = new LocalTime(offsetHours);
var notes = store.Load();
var now = DateTime.UtcNow;

var due = notes.Where(n => n.RemindAtUtc <= now).ToList();
if (due.Count == 0)
{
    Console.WriteLine("[INFO] No due reminders.");
    Environment.Exit(0);
}

var bot = new TelegramBotClient(botToken);
var sentIds = new HashSet<string>();

foreach (var note in due)
{
    var when = localTime.ToLocal(note.RemindAtUtc).ToString("yyyy-MM-dd HH:mm");
    var text = Messages.Reminder(note.Text, when);

    try
    {
        await bot.SendMessage(chatId: note.OwnerUserId, text: text);
        sentIds.Add(note.Id);
        Console.WriteLine($"[INFO] Sent note {note.Id} to {note.OwnerUserId}");
    }
    catch (ApiRequestException ex) when (ex.ErrorCode == 429)
    {
        Console.WriteLine($"[WARN] Rate limited on note {note.Id}, will retry next run: {ex.Message}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[WARN] Failed to send note {note.Id}: {ex.Message}");
    }
}

if (sentIds.Count > 0)
{
    notes.RemoveAll(n => sentIds.Contains(n.Id));
    store.Save(notes);
}

Environment.Exit(0);