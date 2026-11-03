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

// Startup: GetMe with retry, since Telegram occasionally returns 502 on cold start.
// If GetMe still fails after retries, the exception propagates and Railway will
// restart the container — which is correct, because a bot without a verified
// identity cannot serve anyone.
var me = await TelegramApiRetry.ExecuteAsync(
    "GetMe",
    ct => bot.GetMe(ct),
    maxAttempts: 8,
    CancellationToken.None);
Console.WriteLine($"[INFO] Bot started: @{me.Username}");

// Start cron loop: run once every minute.
// Failures inside RunOnceAsync are caught by CronRunner itself; the outer
// try/catch is a last line of defence so the background task never dies.
_ = Task.Run(async () =>
{
    using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
    while (await timer.WaitForNextTickAsync())
    {
        Console.WriteLine("[INFO] Cron tick.");
        try
        {
            await cronRunner.RunOnceAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Cron run failed: {ex}");
        }
    }
});

// Long-polling loop. Transient failures on GetUpdates are retried in-place;
// if retries are exhausted, the loop backs off and tries again instead of
// letting the process die. A single bad update is logged and skipped.
var offset = 0;
while (true)
{
    Update[] updates;
    try
    {
        updates = await TelegramApiRetry.ExecuteAsync(
            "GetUpdates",
            ct => bot.GetUpdates(offset: offset, timeout: 30, cancellationToken: ct),
            maxAttempts: 5,
            CancellationToken.None);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[WARN] GetUpdates failed after retries: {ex.Message}. Backing off 10s...");
        await Task.Delay(TimeSpan.FromSeconds(10));
        continue;
    }

    foreach (var update in updates)
    {
        offset = update.Id + 1;

        try
        {
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
                await Send(bot, from.Id, Messages.Start);
                continue;
            }

            if (text == "/help")
            {
                await Send(bot, from.Id, Messages.Help);
                continue;
            }

            if (text == "/privacy")
            {
                await Send(bot, from.Id, Messages.Privacy);
                continue;
            }

            if (from.Id == bootstrapAdminId && text.StartsWith("/allow ", StringComparison.Ordinal))
            {
                if (long.TryParse(text[7..].Trim(), out var newId))
                {
                    approvalService.Approve(newId);
                    await Send(bot, from.Id, Messages.ApprovedUser(newId));
                }
                else
                {
                    await Send(bot, from.Id, Messages.AllowUsage);
                }
                continue;
            }

            await HandleMessage(bot, reminderService, from.Id, text);
        }
        catch (Exception ex)
        {
            // One bad update must not kill the polling loop.
            Console.WriteLine($"[ERROR] Failed to handle update {update.Id}: {ex}");
        }
    }
}

// --- Helpers -------------------------------------------------------------

/// <summary>
/// Sends a text message with transient-error retry.
/// </summary>
/// <param name="bot">Telegram client.</param>
/// <param name="chatId">Target chat.</param>
/// <param name="text">Message body.</param>
static Task Send(TelegramBotClient bot, long chatId, string text) =>
    TelegramApiRetry.ExecuteAsync(
        "SendMessage",
        ct => bot.SendMessage(chatId: chatId, text: text, cancellationToken: ct),
        maxAttempts: 4,
        CancellationToken.None);

/// <summary>
/// Handles a message from an unapproved user: notifies the admin with
/// approve/reject buttons and tells the user they are waiting for approval.
/// </summary>
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

        await TelegramApiRetry.ExecuteAsync(
            "SendMessage(approval request)",
            ct => bot.SendMessage(
                chatId: adminId,
                text: Messages.ApprovalRequest(userId, text),
                replyMarkup: keyboard,
                cancellationToken: ct),
            maxAttempts: 4,
            CancellationToken.None);
    }

    await Send(bot, userId, Messages.WaitingApproval);
}

/// <summary>
/// Handles an inline-keyboard callback from the bootstrap admin, applying
/// approve/reject decisions and updating the admin's message to remove the buttons.
/// </summary>
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
        await Send(bot, targetId, Messages.Approved);
        await TelegramApiRetry.ExecuteAsync(
            "AnswerCallbackQuery",
            ct => bot.AnswerCallbackQuery(callback.Id, $"Approved {targetId}", cancellationToken: ct),
            maxAttempts: 4,
            CancellationToken.None);
        if (callback.Message is { } msg)
            await TelegramApiRetry.ExecuteAsync(
                "EditMessageReplyMarkup",
                ct => bot.EditMessageReplyMarkup(msg.Chat.Id, msg.MessageId, cancellationToken: ct),
                maxAttempts: 4,
                CancellationToken.None);
    }
    else if (action == "reject")
    {
        approval.Reject(targetId);
        await Send(bot, targetId, Messages.Rejected);
        await TelegramApiRetry.ExecuteAsync(
            "AnswerCallbackQuery",
            ct => bot.AnswerCallbackQuery(callback.Id, $"Rejected {targetId}", cancellationToken: ct),
            maxAttempts: 4,
            CancellationToken.None);
        if (callback.Message is { } msg)
            await TelegramApiRetry.ExecuteAsync(
                "EditMessageReplyMarkup",
                ct => bot.EditMessageReplyMarkup(msg.Chat.Id, msg.MessageId, cancellationToken: ct),
                maxAttempts: 4,
                CancellationToken.None);
    }
}

/// <summary>
/// Handles a text message from an approved user: parses it as a reminder,
/// stores it, and reports either the stored reminder or a user-facing error.
/// </summary>
static async Task HandleMessage(
    TelegramBotClient bot,
    ReminderService reminderService,
    long userId,
    string input)
{
    try
    {
        var reply = reminderService.Create(userId, input);
        await Send(bot, userId, reply);
    }
    catch (FormatException)
    {
        await Send(bot, userId, Messages.ParseError);
    }
    catch (ReminderInPastException ex)
    {
        await Send(
            bot,
            userId,
            Messages.ReminderInPast(ex.LocalDateTime.ToString("yyyy-MM-dd HH:mm")));
    }
}