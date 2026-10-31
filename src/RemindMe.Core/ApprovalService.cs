namespace RemindMe.Core;

public sealed class ApprovalService
{
    private readonly JsonLongSetStore _users;
    private readonly JsonLongSetStore _pending;

    public ApprovalService(JsonLongSetStore users, JsonLongSetStore pending)
    {
        _users = users;
        _pending = pending;
    }

    /// <summary>Returns true if user was newly added to pending.</summary>
    public bool RequestApproval(long userId)
    {
        var pending = _pending.Load();
        if (pending.Contains(userId)) return false;
        pending.Add(userId);
        _pending.Save(pending);
        return true;
    }

    public void Approve(long userId)
    {
        var users = _users.Load();
        var pending = _pending.Load();
        users.Add(userId);
        pending.Remove(userId);
        _users.Save(users);
        _pending.Save(pending);
    }

    public void Reject(long userId)
    {
        var pending = _pending.Load();
        pending.Remove(userId);
        _pending.Save(pending);
    }

    public bool IsApproved(long userId) => _users.Load().Contains(userId);
    public bool IsPending(long userId) => _pending.Load().Contains(userId);
}