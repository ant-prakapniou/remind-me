using Microsoft.Extensions.Configuration;
using RemindMe.Core;
using Telegram.Bot;
using Telegram.Bot.Types;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var botToken = config["Telegram:BotToken"]
    ?? throw new InvalidOperationException("Telegram:BotToken is not configured");
var allowedUserId = config.GetValue<long>("Telegram:AllowedUserId");
var notesPath = config["Storage:NotesFilePath"]
    ?? throw new InvalidOperationException("Storage:NotesFilePath is not configured");

var store = new NoteStore(notesPath);
var bot = new TelegramBotClient(botToken);

var me = await bot.GetMe();
Console.WriteLine($"Bot started: @{me.Username}");

var offset = 0;
while (true)
{
    var updates = await bot.GetUpdates(offset: offset, timeout: 30);
    foreach (var update in updates)
    {
        offset = update.Id + 1;
        if (update.Message is not { From: { } from, Text: { } text }) continue;
        if (from.Id != allowedUserId) continue;

        await HandleMessage(bot, store, from.Id, text);
    }
}

static async Task HandleMessage(TelegramBotClient bot, NoteStore store, long userId, string input)
{
    string? message;
    DateTime dtLocal;
    try
    {
        (message, dtLocal) = DateTimeTailParser.Parse(input);
    }
    catch (FormatException)
    {
        await bot.SendMessage(
            chatId: userId,
            text: "Could not parse. Format: <text> HH MM DD MM YYYY\n" +
                  "Example: buy bread 17 00 21 06 2026\n\n" +
                  "Note: the note text must not end with a digit.");
        return;
    }

    var dtUtc = DateTime.SpecifyKind(dtLocal.AddHours(-3), DateTimeKind.Utc);

    var note = new Note
    {
        Id = Guid.NewGuid().ToString("N")[..8],
        OwnerUserId = userId,
        Text = message ?? string.Empty,
        RemindAtUtc = dtUtc
    };

    var notes = store.Load();
    notes.Add(note);
    store.Save(notes);

    var when = dtLocal.ToString("yyyy-MM-dd HH:mm");
    var reply = string.IsNullOrEmpty(note.Text)
        ? $"OK, reminder set.\nWhen: {when}"
        : $"OK, reminder set: \"{note.Text}\"\nWhen: {when}";

    await bot.SendMessage(chatId: userId, text: reply);
}