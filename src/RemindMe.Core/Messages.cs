namespace RemindMe.Core;

public static class Messages
{
    public static string ReminderSet(string? text, string when) =>
        string.IsNullOrEmpty(text)
            ? $"OK, reminder set.\nWhen: {when}"
            : $"OK, reminder set: \"{text}\"\nWhen: {when}";

    public static string Reminder(string? text, string when) =>
        string.IsNullOrEmpty(text)
            ? $"Reminder.\nWhen: {when}"
            : $"Reminder: \"{text}\"\nWhen: {when}";

    public const string ParseError =
        "Could not parse. Format: <text> HH MM DD MM YYYY\n" +
        "Example: buy bread 17 00 21 06 2026\n\n" +
        "Note: the note text must not end with a digit.";

    public const string Start =
        "Hi! I'm a simple reminder bot.\n\n" +
        "Send me: <text> HH MM DD MM YYYY\n" +
        "Example: buy bread 17 00 21 06 2026\n\n" +
        "Commands:\n" +
        "/help — format and examples\n" +
        "/privacy — what I store";

    public const string Help =
        "Format: <text> HH MM DD MM YYYY\n" +
        "The date and time go at the END of the message.\n" +
        "Separators between digits can be anything (space, dot, dash, colon).\n\n" +
        "Examples:\n" +
        "buy bread 17 00 21 06 2026\n" +
        "call mom 09:30 26 10 2026\n" +
        "17 00 21 06 2026  (reminder without text)\n\n" +
        "Year must be 4 digits. The note text must not end with a digit.";

    public const string Privacy =
        "This bot stores:\n" +
        "- your Telegram user ID\n" +
        "- the text and datetime of reminders you create\n\n" +
        "Data is not shared with third parties.\n" +
        "Standard Telegram Bot Privacy Policy applies:\n" +
        "https://telegram.org/privacy-tpa";

    public const string WaitingApproval =
        "Waiting for approval. You will be notified once the admin responds.";

    public const string Approved = "Approved. Write your reminder.";
    public const string Rejected = "Rejected by admin.";

    public static string ApprovalRequest(long userId, string firstMessage)
    {
        var name = firstMessage.Length > 40 ? firstMessage[..40] + "…" : firstMessage;
        return $"User {userId} wants to use the bot.\nFirst message: \"{name}\"\nApprove?";
    }

    public static string ApprovedUser(long userId) => $"Approved user {userId}.";

    public const string AllowUsage = "Usage: /allow <userId>";

    public static string ReminderInPast(string when) =>
        $"Could not set reminder: {when} is in the past.\n" +
        "Please pick a future date and time.";
}