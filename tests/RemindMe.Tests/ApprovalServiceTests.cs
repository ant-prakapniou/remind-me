using RemindMe.Core;

namespace RemindMe.Tests;

public class ApprovalServiceTests : IDisposable
{
    private readonly string _usersPath;
    private readonly string _pendingPath;
    private readonly ApprovalService _service;

    public ApprovalServiceTests()
    {
        _usersPath = Path.Combine(Path.GetTempPath(), $"users_{Guid.NewGuid():N}.json");
        _pendingPath = Path.Combine(Path.GetTempPath(), $"pending_{Guid.NewGuid():N}.json");
        _service = new ApprovalService(
            new JsonLongSetStore(_usersPath),
            new JsonLongSetStore(_pendingPath));
    }

    public void Dispose()
    {
        if (File.Exists(_usersPath)) File.Delete(_usersPath);
        if (File.Exists(_usersPath + ".tmp")) File.Delete(_usersPath + ".tmp");
        if (File.Exists(_pendingPath)) File.Delete(_pendingPath);
        if (File.Exists(_pendingPath + ".tmp")) File.Delete(_pendingPath + ".tmp");
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void NewUser_NotApproved_NotPending()
    {
        Assert.False(_service.IsApproved(1));
        Assert.False(_service.IsPending(1));
    }

    [Fact]
    public void RequestApproval_AddsToPending()
    {
        Assert.True(_service.RequestApproval(1));
        Assert.True(_service.IsPending(1));
        Assert.False(_service.IsApproved(1));
    }

    [Fact]
    public void RequestApproval_Repeated_ReturnsFalse()
    {
        _service.RequestApproval(1);
        Assert.False(_service.RequestApproval(1));
    }

    [Fact]
    public void Approve_MovesPendingToApproved()
    {
        _service.RequestApproval(1);
        _service.Approve(1);

        Assert.True(_service.IsApproved(1));
        Assert.False(_service.IsPending(1));
    }

    [Fact]
    public void Approve_WithoutPending_AddsAnyway()
    {
        _service.Approve(1);
        Assert.True(_service.IsApproved(1));
    }

    [Fact]
    public void Reject_RemovesFromPending()
    {
        _service.RequestApproval(1);
        _service.Reject(1);

        Assert.False(_service.IsPending(1));
        Assert.False(_service.IsApproved(1));
    }
}