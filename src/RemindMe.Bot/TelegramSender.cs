using Telegram.Bot;

namespace RemindMe.Bot;

/// <summary>
/// Production implementation of <see cref="ITelegramSender"/> backed by <see cref="TelegramBotClient"/>.
/// </summary>
/// <param name="bot">The Telegram client used to actually send messages.</param>
public sealed class TelegramSender(TelegramBotClient bot) : ITelegramSender
{
    /// <inheritdoc />
    public Task SendMessageAsync(long chatId, string text, CancellationToken ct) =>
        bot.SendMessage(chatId: chatId, text: text, cancellationToken: ct);
}