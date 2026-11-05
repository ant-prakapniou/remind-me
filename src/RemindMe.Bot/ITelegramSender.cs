namespace RemindMe.Bot;

/// <summary>
/// Sends a text message to a Telegram chat.
/// </summary>
/// <remarks>
/// <para>
/// Abstracts <see cref="Telegram.Bot.TelegramBotClient"/> so <see cref="CronRunner"/>
/// can be tested without a real bot. The production implementation is
/// <see cref="TelegramSender"/>.
/// </para>
/// <para>
/// Only the single operation needed by <see cref="CronRunner"/> is exposed.
/// Handlers in <c>Program.cs</c> keep using <see cref="Telegram.Bot.TelegramBotClient"/>
/// directly, because they need the full API (callbacks, reply markup, edits).
/// </para>
/// </remarks>
public interface ITelegramSender
{
    /// <summary>
    /// Sends <paramref name="text"/> to the chat with the given <paramref name="chatId"/>.
    /// </summary>
    /// <param name="chatId">Telegram chat ID (for private chats, the user ID).</param>
    /// <param name="text">Message body.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <returns>A task that completes when Telegram accepts the message.</returns>
    /// <exception cref="Telegram.Bot.Exceptions.ApiRequestException">
    /// Telegram rejected the message. The <c>ErrorCode</c> distinguishes
    /// permanent failures (400, 403) from transient ones (429, 5xx).
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// Network-level failure (DNS, TLS, connection reset).
    /// </exception>
    /// <exception cref="TaskCanceledException">
    /// The request timed out or was cancelled via <paramref name="ct"/>.
    /// </exception>
    Task SendMessageAsync(long chatId, string text, CancellationToken ct);
}