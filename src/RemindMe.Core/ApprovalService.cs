namespace RemindMe.Core;

/// <summary>
/// Manages the approval lifecycle for bot users: pending requests,
/// approved users, and rejections.
/// </summary>
/// <remarks>
/// <para>
/// The bot is invitation-by-approval: a new user who writes to the bot is
/// placed into a "pending" list and the admin is notified. The admin either
/// approves (user moves to "approved") or rejects (user is removed from
/// pending). Only approved users can create reminders.
/// </para>
/// <para>
/// Approved and pending users are stored in two separate JSON files via
/// <see cref="JsonLongSetStore"/> (<c>users.json</c> and <c>pending.json</c>).
/// The service does not send any Telegram messages — notification logic lives
/// in the bot.
/// </para>
/// </remarks>
/// <param name="users">Store for the set of approved user IDs.</param>
/// <param name="pending">Store for the set of user IDs awaiting approval.</param>
public sealed class ApprovalService(JsonLongSetStore users, JsonLongSetStore pending)
{
    private readonly JsonLongSetStore _users = users;
    private readonly JsonLongSetStore _pending = pending;

    /// <summary>
    /// Adds the user to the pending list if they are not already there.
    /// </summary>
    /// <param name="userId">Telegram user ID of the requester.</param>
    /// <returns>
    /// <see langword="true"/> if the user was newly added to pending
    /// (and the admin should be notified);
    /// <see langword="false"/> if the user was already pending
    /// (no notification needed — prevents spamming the admin on repeated messages).
    /// </returns>
    /// <exception cref="IOException">
    /// The pending file could not be written.
    /// </exception>
    public bool RequestApproval(long userId)
    {
        var pending = _pending.Load();
        if (pending.Contains(userId)) return false;
        pending.Add(userId);
        _pending.Save(pending);
        return true;
    }

    /// <summary>
    /// Approves a user: adds them to the approved list and removes them from pending.
    /// </summary>
    /// <param name="userId">Telegram user ID to approve.</param>
    /// <remarks>
    /// Safe to call for a user who is not in pending (e.g. when the admin
    /// adds a user manually via <c>/allow</c>). In that case the user is
    /// simply added to approved.
    /// </remarks>
    /// <exception cref="IOException">
    /// Either the users or pending file could not be written.
    /// </exception>
    public void Approve(long userId)
    {
        var users = _users.Load();
        var pending = _pending.Load();
        users.Add(userId);
        pending.Remove(userId);
        _users.Save(users);
        _pending.Save(pending);
    }

    /// <summary>
    /// Rejects a user: removes them from the pending list.
    /// </summary>
    /// <param name="userId">Telegram user ID to reject.</param>
    /// <remarks>
    /// The user is not added to any list. If they write to the bot again,
    /// they will be placed back into pending and the admin will be notified
    /// again — rejection is not a permanent ban.
    /// </remarks>
    /// <exception cref="IOException">
    /// The pending file could not be written.
    /// </exception>
    public void Reject(long userId)
    {
        var pending = _pending.Load();
        pending.Remove(userId);
        _pending.Save(pending);
    }

    /// <summary>
    /// Checks whether the user is in the approved list.
    /// </summary>
    /// <param name="userId">Telegram user ID to check.</param>
    /// <returns><see langword="true"/> if the user is approved.</returns>
    public bool IsApproved(long userId) => _users.Load().Contains(userId);

    /// <summary>
    /// Checks whether the user is in the pending list.
    /// </summary>
    /// <param name="userId">Telegram user ID to check.</param>
    /// <returns><see langword="true"/> if the user is awaiting approval.</returns>
    public bool IsPending(long userId) => _pending.Load().Contains(userId);
}