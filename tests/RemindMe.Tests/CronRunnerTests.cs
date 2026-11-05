using RemindMe.Bot;
using RemindMe.Core;
using Telegram.Bot.Exceptions;

namespace RemindMe.Tests.Telegram;

public class CronRunnerTests : IDisposable
{
    private readonly string _path;
    private readonly NoteStore _store;
    private readonly FakeSender _sender;
    private readonly CronRunner _runner;

    public CronRunnerTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"notes_{Guid.NewGuid():N}.json");
        _store = new NoteStore(_path);
        _sender = new FakeSender();
        _runner = new CronRunner(_store, new LocalTime(3), _sender, maxSendAttempts: 1);
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
        if (File.Exists(_path + ".tmp")) File.Delete(_path + ".tmp");
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task RunOnce_NoDueNotes_DoesNothing()
    {
        _store.Save([MakeNote("future", DateTime.UtcNow.AddHours(1))]);

        await _runner.RunOnceAsync();

        Assert.Empty(_sender.Sent);
        Assert.Single(_store.Load());
    }

    [Fact]
    public async Task RunOnce_DueNote_SendsAndRemoves()
    {
        _store.Save([MakeNote("due", DateTime.UtcNow.AddMinutes(-1))]);

        await _runner.RunOnceAsync();

        var (ChatId, Text)= Assert.Single(_sender.Sent);
        Assert.Equal(1, ChatId);
        Assert.Contains("due", Text);
    }

    [Fact]
    public async Task RunOnce_PermanentFailure_DropsNote()
    {
        _store.Save([MakeNote("dead", DateTime.UtcNow.AddMinutes(-1))]);
        _sender.Enqueue(new ApiRequestException("chat not found", 400));

        await _runner.RunOnceAsync();

        Assert.Empty(_sender.Sent);      // nothing delivered
        Assert.Empty(_store.Load());     // but the note is gone
    }

    [Fact]
    public async Task RunOnce_TransientFailure_KeepsNote()
    {
        _store.Save([MakeNote("alive", DateTime.UtcNow.AddMinutes(-1))]);
        // 429 after retries — TelegramApiRetry will retry, then throw 429.
        _sender.Enqueue(new ApiRequestException("too many requests", 429));

        await _runner.RunOnceAsync();

        Assert.Empty(_sender.Sent);
        Assert.Single(_store.Load());    // kept for the next tick
    }

    [Fact]
    public async Task RunOnce_MixedOutcomes_AppliesEachPolicyIndependently()
    {
        _store.Save([
            MakeNote("good",   DateTime.UtcNow.AddMinutes(-1)),
            MakeNote("dead",   DateTime.UtcNow.AddMinutes(-1)),
            MakeNote("alive",  DateTime.UtcNow.AddMinutes(-1)),
        ]);
        _sender.Enqueue(null);                               // good: sent
        _sender.Enqueue(new ApiRequestException("blocked", 403)); // dead: dropped
        _sender.Enqueue(new ApiRequestException("rate", 429));    // alive: kept

        await _runner.RunOnceAsync();

        var remaining = _store.Load();
        Assert.Single(remaining);
        Assert.Equal("alive", remaining[0].Id);
    }

    [Theory]
    [InlineData(400, true)]
    [InlineData(403, true)]
    [InlineData(429, false)]
    [InlineData(500, false)]
    [InlineData(502, false)]
    public void IsPermanentDeliveryFailure_ClassifiesErrorCodes(int errorCode, bool expected)
    {
        Assert.Equal(expected, CronRunner.IsPermanentDeliveryFailure(errorCode));
    }

    private static Note MakeNote(string id, DateTime remindAtUtc) => new()
    {
        Id = id,
        OwnerUserId = 1,
        Text = id,
        RemindAtUtc = remindAtUtc
    };

    /// <summary>
    /// Test double for <see cref="ITelegramSender"/>. Each queued item is either
    /// <c>null</c> (success) or an exception to throw on that call.
    /// </summary>
    private sealed class FakeSender : ITelegramSender
    {
        private readonly Queue<Exception?> _responses = new();

        public List<(long ChatId, string Text)> Sent { get; } = [];

        public void Enqueue(Exception? response) => _responses.Enqueue(response);

        public Task SendMessageAsync(long chatId, string text, CancellationToken ct)
        {
            var response = _responses.Count > 0 ? _responses.Dequeue() : null;
            if (response is not null) throw response;
            Sent.Add((chatId, text));
            return Task.CompletedTask;
        }
    }
}