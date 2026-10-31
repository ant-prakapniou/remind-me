using Microsoft.Extensions.Configuration;
using RemindMe.Bot;
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
var reminderService = new ReminderService(noteStore, localTime);
var approvalService = new ApprovalService(userStore, pendingStore);
var bot = new TelegramBotClient(botToken);

if (!approvalService.IsApproved(bootstrapAdminId))
    approvalService.Approve(bootstrapAdminId);

var cronRunner = new CronRunner(noteStore, localTime, bot);

var me = await bot.GetMe();
Console.WriteLine($"[INFO] Bot started: @{me.Username}");

// Start cron loop: run once every 5 minutes
_ = Task.Run(async () =>
{
    using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
    while (await timer.WaitForNextTickAsync())
    {
        Console.WriteLine("[INFO] Cron tick.");
        await cronRunner.RunOnceAsync();
    }
});

var offset = 0;
while (true)
{
    var updates = await bot.GetUpdates(offset: offset, timeout: 30);
    foreach (var update in updates)
    {
        offset = update.Id + 1;

        if (update.CallbackQuery is { } callback)
        {
            await HandleCallback(bot, approvalService, bootstrapAdminId, callback);
            continue;
        }

        if (update.Message is not { From: { } from, Text: { } text }) continue;

        if (!approvalService.IsApproved(from.Id))
        {
            await HandleNewUser(bot, approvalService, bootstrapAdminId, from.Id, text);
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
                approvalService.Approve(newId);
                await bot.SendMessage(from.Id, Messages.ApprovedUser(newId));
            }
            else
            {
                await bot.SendMessage(from.Id, Messages.AllowUsage);
            }
            continue;
        }

        await HandleMessage(bot, reminderService, from.Id, text);
    }
}

static async Task HandleNewUser(
    TelegramBotClient bot,
    ApprovalService approval,
    long adminId,
    long userId,
    string text)
{
    if (approval.RequestApproval(userId))
    {
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
    ApprovalService approval,
    long adminId,
    CallbackQuery callback)
{
    if (callback.From.Id != adminId) return;
    if (callback.Data is not { } data) return;

    var parts = data.Split(':');
    if (parts.Length != 2 || !long.TryParse(parts[1], out var targetId)) return;

    var action = parts[0];
    if (!approval.IsPending(targetId)) return;

    if (action == "approve")
    {
        approval.Approve(targetId);
        await bot.SendMessage(targetId, Messages.Approved);
        await bot.AnswerCallbackQuery(callback.Id, $"Approved {targetId}");
        if (callback.Message is { } msg)
            await bot.EditMessageReplyMarkup(msg.Chat.Id, msg.MessageId);
    }
    else if (action == "reject")
    {
        approval.Reject(targetId);
        await bot.SendMessage(targetId, Messages.Rejected);
        await bot.AnswerCallbackQuery(callback.Id, $"Rejected {targetId}");
        if (callback.Message is { } msg)
            await bot.EditMessageReplyMarkup(msg.Chat.Id, msg.MessageId);
    }
}

static async Task HandleMessage(
    TelegramBotClient bot,
    ReminderService reminderService,
    long userId,
    string input)
{
    try
    {
        var reply = reminderService.Create(userId, input);
        await bot.SendMessage(chatId: userId, text: reply);
    }
    catch (FormatException)
    {
        await bot.SendMessage(chatId: userId, text: Messages.ParseError);
    }
    catch (ReminderInPastException ex)
    {
        await bot.SendMessage(
            chatId: userId,
            text: Messages.ReminderInPast(ex.LocalDateTime.ToString("yyyy-MM-dd HH:mm")));
    }
}