namespace RemindMe.Core;

/// <summary>
/// Centralized collection of all user-facing text sent by the bot and cron job.
/// </summary>
/// <remarks>
/// <para>
/// Every string delivered to a Telegram user originates from this class.
/// Keeping messages in one place makes it easy to change wording, translate
/// to another language, or audit what the bot tells users.
/// </para>
/// <para>
/// All messages are in English. User input (note text) is passed through
/// verbatim and is not translated.
/// </para>
/// <para>
/// Constants (<c>const string</c>) are compile-time literals for fixed messages.
/// Methods are used where the message depends on runtime values
/// (note text, timestamp, user ID).
/// </para>
/// </remarks>
public static class Messages
{
    /// <summary>
    /// Confirmation sent by the bot after a reminder is successfully created.
    /// </summary>
    /// <param name="text">
    /// The note text entered by the user, or <see langword="null"/> / empty
    /// for a "bare alarm" without text.
    /// </param>
    /// <param name="when">
    /// The formatted local date and time of the reminder, e.g. <c>2026-06-21 17:00</c>.
    /// </param>
    /// <returns>
    /// A message containing the reminder text (if any) and the scheduled time.
    /// If <paramref name="text"/> is empty, the message omits the quoted text.
    /// </returns>
    public static string ReminderSet(string? text, string when) =>
        string.IsNullOrEmpty(text)
            ? $"OK, reminder set.\nWhen: {when}"
            : $"OK, reminder set: \"{text}\"\nWhen: {when}";

    /// <summary>
    /// The reminder delivered by the cron job when a note becomes due.
    /// </summary>
    /// <param name="text">
    /// The note text, or <see langword="null"/> / empty for a bare alarm.
    /// </param>
    /// <param name="when">
    /// The formatted local date and time of the reminder, e.g. <c>2026-06-21 17:00</c>.
    /// </param>
    /// <returns>
    /// A message containing the reminder text (if any) and the scheduled time.
    /// If <paramref name="text"/> is empty, the message omits the quoted text.
    /// </returns>
    public static string Reminder(string? text, string when) =>
        string.IsNullOrEmpty(text)
            ? $"Reminder.\nWhen: {when}"
            : $"Reminder: \"{text}\"\nWhen: {when}";

    /// <summary>
    /// Error message shown when the user's input cannot be parsed
    /// as <c>&lt;text&gt; HH MM DD MM YYYY</c>.
    /// </summary>
    /// <remarks>
    /// Includes the expected format, an example, and a hint about the
    /// most common mistake: note text ending with a digit.
    /// </remarks>
    public const string ParseError =
        "Could not parse. Format: <text> HH MM DD MM YYYY\n" +
        "Example: buy bread 17 00 21 06 2026\n\n" +
        "Note: the note text must not end with a digit.";

    /// <summary>
    /// Greeting shown in response to the <c>/start</c> command.
    /// </summary>
    /// <remarks>
    /// Sent only to approved users. Unapproved users receive
    /// <see cref="WaitingApproval"/> instead.
    /// </remarks>
    public const string Start =
        "Hi! I'm a simple reminder bot.\n\n" +
        "Send me: <text> HH MM DD MM YYYY\n" +
        "Example: buy bread 17 00 21 06 2026\n\n" +
        "Commands:\n" +
        "/help — format and examples\n" +
        "/privacy — what I store";

    /// <summary>
    /// Detailed format description shown in response to the <c>/help</c> command.
    /// </summary>
    /// <remarks>
    /// Explains that the date and time must be at the end of the message,
    /// that any non-digit characters can act as separators, and that the
    /// year must be exactly four digits.
    /// </remarks>
    public const string Help =
        "Format: <text> HH MM DD MM YYYY\n" +
        "The date and time go at the END of the message.\n" +
        "Separators between digits can be anything (space, dot, dash, colon).\n\n" +
        "Examples:\n" +
        "buy bread 17 00 21 06 2026\n" +
        "call mom 09:30 26 10 2026\n" +
        "17 00 21 06 2026  (reminder without text)\n\n" +
        "Year must be 4 digits. The note text must not end with a digit.";

    /// <summary>
    /// Privacy notice shown in response to the <c>/privacy</c> command.
    /// </summary>
    /// <remarks>
    /// Telegram requires every bot to expose a privacy policy. This message
    /// serves as that policy and links to the standard Telegram Bot Privacy Policy.
    /// </remarks>
    public const string Privacy =
        "This bot stores:\n" +
        "- your Telegram user ID\n" +
        "- the text and datetime of reminders you create\n\n" +
        "Data is not shared with third parties.\n" +
        "Standard Telegram Bot Privacy Policy applies:\n" +
        "https://telegram.org/privacy-tpa";

    /// <summary>
    /// Message sent to a new user whose approval request has just been submitted.
    /// </summary>
    /// <remarks>
    /// The user is not yet allowed to create reminders. Once the admin
    /// approves or rejects the request, the user receives
    /// <see cref="Approved"/> or <see cref="Rejected"/>.
    /// </remarks>
    public const string WaitingApproval =
        "Waiting for approval. You will be notified once the admin responds.";

    /// <summary>
    /// Message sent to a user whose approval request was accepted by the admin.
    /// </summary>
    public const string Approved = "Approved. Write your reminder.";

    /// <summary>
    /// Message sent to a user whose approval request was rejected by the admin.
    /// </summary>
    public const string Rejected = "Rejected by admin.";

    /// <summary>
    /// Notification sent to the admin when a new user requests access to the bot.
    /// </summary>
    /// <param name="userId">Telegram user ID of the requester.</param>
    /// <param name="firstMessage">
    /// The first message the requester sent, used as context for the admin.
    /// Truncated to 40 characters with an ellipsis if longer.
    /// </param>
    /// <returns>
    /// A message with the requester's ID, their (possibly truncated) first
    /// message, and a prompt to approve or reject via inline buttons.
    /// </returns>
    public static string ApprovalRequest(long userId, string firstMessage)
    {
        var name = firstMessage.Length > 40 ? firstMessage[..40] + "…" : firstMessage;
        return $"User {userId} wants to use the bot.\nFirst message: \"{name}\"\nApprove?";
    }

    /// <summary>
    /// Confirmation sent to the admin after a user is approved via the
    /// <c>/allow</c> command.
    /// </summary>
    /// <param name="userId">Telegram user ID that was approved.</param>
    /// <returns>A short confirmation message.</returns>
    public static string ApprovedUser(long userId) => $"Approved user {userId}.";

    /// <summary>
    /// Usage hint shown when the <c>/allow</c> command is called without
    /// a valid user ID argument.
    /// </summary>
    public const string AllowUsage = "Usage: /allow <userId>";

    /// <summary>
    /// Error message shown when the user tries to set a reminder for a
    /// date and time in the past.
    /// </summary>
    /// <param name="when">
    /// The formatted local date and time that was rejected,
    /// e.g. <c>2020-06-21 17:00</c>.
    /// </param>
    /// <returns>
    /// A message explaining that the given time is in the past and asking
    /// the user to pick a future date and time.
    /// </returns>
    public static string ReminderInPast(string when) =>
        $"Could not set reminder: {when} is in the past.\n" +
        "Please pick a future date and time.";
}