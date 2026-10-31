using Microsoft.Extensions.Configuration;
using RemindMe.Core;
using Telegram.Bot;

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

var store = new NoteStore(notesPath);
var notes = store.Load();
var now = DateTime.UtcNow;

var due = notes.Where(n => n.RemindAtUtc <= now).ToList();
if (due.Count == 0)
{
    Console.WriteLine("No due reminders.");
    Environment.Exit(0);
}

var bot = new TelegramBotClient(botToken);
var sentIds = new HashSet<string>();

foreach (var note in due)
{
    var when = note.RemindAtUtc.AddHours(3).ToString("yyyy-MM-dd HH:mm");
    var text = string.IsNullOrEmpty(note.Text)
        ? $"Reminder.\nWhen: {when}"
        : $"Reminder: \"{note.Text}\"\nWhen: {when}";

    try
    {
        await bot.SendMessage(chatId: note.OwnerUserId, text: text);
        sentIds.Add(note.Id);
        Console.WriteLine($"Sent note {note.Id} to {note.OwnerUserId}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to send note {note.Id}: {ex.Message}");
    }
}

if (sentIds.Count > 0)
{
    notes.RemoveAll(n => sentIds.Contains(n.Id));
    store.Save(notes);
}

Environment.Exit(0);