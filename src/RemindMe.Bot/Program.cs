using Microsoft.Extensions.Configuration;
using RemindMe.Core;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var botToken = config["Telegram:BotToken"]
    ?? throw new InvalidOperationException("Telegram:BotToken is not configured");
var bootstrapAdminId = config.GetValue<long>("Telegram:BootstrapAdminId");
var notesPath = config["Storage:NotesFilePath"]
    ?? throw new InvalidOperationException("Storage:NotesFilePath is not configured");
var usersPath = config["Storage:UsersFilePath"]
    ?? throw new InvalidOperationException("Storage:UsersFilePath is not configured");
var pendingPath = config["Storage:PendingFilePath"]
    ?? throw new InvalidOperationException("Storage:PendingFilePath is not configured");
var offsetHours = config.GetValue<int?>("LocalTime:OffsetHours") ?? 3;

if (bootstrapAdminId == 0)
    throw new InvalidOperationException("Telegram:BootstrapAdminId is not configured");

var noteStore = new NoteStore(notesPath);
var userStore = new JsonLongSetStore(usersPath);
var pendingStore = new JsonLongSetStore(pendingPath);
var localTime = new LocalTime(offsetHours);
var bot = new TelegramBotClient(botToken);

var users = userStore.Load();
if (!users.Contains(bootstrapAdminId))
{
    users.Add(bootstrapAdminId);
    userStore.Save(users);
}

var me = await bot.GetMe();
Console.WriteLine($"[INFO] Bot started: @{me.Username}");

var offset = 0;
while (true)
{
    var updates = await bot.GetUpdates(offset: offset, timeout: 30);
    foreach (var update in updates)
    {
        offset = update.Id + 1;

        if (update.CallbackQuery is { } callback)
        {
            await HandleCallback(bot, userStore, pendingStore, bootstrapAdminId, callback);
            continue;
        }

        if (update.Message is not { From: { } from, Text: { } text }) continue;

        var approved = userStore.Load();
        if (!approved.Contains(from.Id))
        {
            await HandleNewUser(bot, userStore, pendingStore, bootstrapAdminId, from.Id, text);
            continue;
        }

        if (text == "/start")
        {
            await bot.SendMessage(from.Id, Messages.Start);
            continue;
        }

        if (text == "/help")
        {
            await bot.SendMessage(from.Id, Messages.Help);
            continue;
        }

        if (text == "/privacy")
        {
            await bot.SendMessage(from.Id, Messages.Privacy);
            continue;
        }

        if (from.Id == bootstrapAdminId && text.StartsWith("/allow ", StringComparison.Ordinal))
        {
            if (long.TryParse(text[7..].Trim(), out var newId))
            {
                var u = userStore.Load();
                var p = pendingStore.Load();
                u.Add(newId);
                p.Remove(newId);
                userStore.Save(u);
                pendingStore.Save(p);
                await bot.SendMessage(from.Id, Messages.ApprovedUser(newId));
            }
            else
            {
                await bot.SendMessage(from.Id, Messages.AllowUsage);
            }
            continue;
        }

        await HandleMessage(bot, noteStore, localTime, from.Id, text);
    }
}

static async Task HandleNewUser(
    TelegramBotClient bot,
    JsonLongSetStore userStore,
    JsonLongSetStore pendingStore,
    long adminId,
    long userId,
    string text)
{
    var pending = pendingStore.Load();
    if (!pending.Contains(userId))
    {
        pending.Add(userId);
        pendingStore.Save(pending);

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Approve", $"approve:{userId}"),
                InlineKeyboardButton.WithCallbackData("Reject",  $"reject:{userId}")
            }
        });

        await bot.SendMessage(
            chatId: adminId,
            text: Messages.ApprovalRequest(userId, text),
            replyMarkup: keyboard);
    }

    await bot.SendMessage(chatId: userId, text: Messages.WaitingApproval);
}

static async Task HandleCallback(
    TelegramBotClient bot,
    JsonLongSetStore userStore,
    JsonLongSetStore pendingStore,
    long adminId,
    CallbackQuery callback)
{
    if (callback.From.Id != adminId) return;
    if (callback.Data is not { } data) return;

    var parts = data.Split(':');
    if (parts.Length != 2 || !long.TryParse(parts[1], out var targetId)) return;

    var action = parts[0];
    var pending = pendingStore.Load();
    if (!pending.Contains(targetId)) return;

    if (action == "approve")
    {
        var users = userStore.Load();
        users.Add(targetId);
        pending.Remove(targetId);
        userStore.Save(users);
        pendingStore.Save(pending);

        await bot.SendMessage(targetId, Messages.Approved);
        await bot.AnswerCallbackQuery(callback.Id, $"Approved {targetId}");
        if (callback.Message is { } msg)
            await bot.EditMessageReplyMarkup(msg.Chat.Id, msg.MessageId);
    }
    else if (action == "reject")
    {
        pending.Remove(targetId);
        pendingStore.Save(pending);

        await bot.SendMessage(targetId, Messages.Rejected);
        await bot.AnswerCallbackQuery(callback.Id, $"Rejected {targetId}");
        if (callback.Message is { } msg)
            await bot.EditMessageReplyMarkup(msg.Chat.Id, msg.MessageId);
    }
}

static async Task HandleMessage(
    TelegramBotClient bot,
    NoteStore store,
    LocalTime localTime,
    long userId,
    string input)
{
    string? message;
    DateTime dtLocal;
    try
    {
        (message, dtLocal) = DateTimeTailParser.Parse(input);
    }
    catch (FormatException)
    {
        await bot.SendMessage(chatId: userId, text: Messages.ParseError);
        return;
    }

    var note = new Note
    {
        Id = Guid.NewGuid().ToString("N")[..8],
        OwnerUserId = userId,
        Text = message ?? string.Empty,
        RemindAtUtc = localTime.ToUtc(dtLocal)
    };

    var notes = store.Load();
    notes.Add(note);
    store.Save(notes);

    var when = dtLocal.ToString("yyyy-MM-dd HH:mm");
    await bot.SendMessage(chatId: userId, text: Messages.ReminderSet(note.Text, when));
}