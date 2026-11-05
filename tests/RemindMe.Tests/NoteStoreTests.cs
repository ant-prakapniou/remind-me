using RemindMe.Core;

namespace RemindMe.Tests;

public class NoteStoreTests : IDisposable
{
    private readonly string _path;

    public NoteStoreTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"notes_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
        if (File.Exists(_path + ".tmp")) File.Delete(_path + ".tmp");
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Load_ReturnsEmpty_WhenFileMissing()
    {
        var store = new NoteStore(_path);
        Assert.Empty(store.Load());
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var store = new NoteStore(_path);
        var note = new Note
        {
            Id = "abc12345",
            OwnerUserId = 42,
            Text = "hello",
            RemindAtUtc = new DateTime(2026, 6, 21, 14, 0, 0, DateTimeKind.Utc)
        };

        store.Save([note]);
        var loaded = store.Load();

        Assert.Single(loaded);
        Assert.Equal("abc12345", loaded[0].Id);
        Assert.Equal(42, loaded[0].OwnerUserId);
        Assert.Equal("hello", loaded[0].Text);
        Assert.Equal(note.RemindAtUtc, loaded[0].RemindAtUtc);
    }

    [Fact]
    public void Save_OverwritesExisting()
    {
        var store = new NoteStore(_path);
        store.Save([MakeNote("1")]);
        store.Save([MakeNote("2")]);

        var loaded = store.Load();
        Assert.Single(loaded);
        Assert.Equal("2", loaded[0].Id);
    }

    [Fact]
    public void Save_CreatesDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"remind_{Guid.NewGuid():N}");
        var path = Path.Combine(dir, "notes.json");
        try
        {
            new NoteStore(path).Save(new List<Note> { MakeNote("1") });
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Update_AppliesMutation_AndPersists()
    {
        var path = Path.Combine(Path.GetTempPath(), $"notes_{Guid.NewGuid():N}.json");
        try
        {
            var store = new NoteStore(path);
            var note = new Note
            {
                Id = "abc12345",
                OwnerUserId = 42,
                Text = "test",
                RemindAtUtc = DateTime.UtcNow.AddHours(1)
            };

            store.Update(notes => notes.Add(note));

            var loaded = store.Load();
            Assert.Single(loaded);
            Assert.Equal("abc12345", loaded[0].Id);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Update_ConcurrentAdds_AreNotLost()
    {
        var path = Path.Combine(Path.GetTempPath(), $"notes_{Guid.NewGuid():N}.json");
        try
        {
            var store = new NoteStore(path);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var ct = TestContext.Current.CancellationToken;

            var t1 = Task.Run(async () =>
            {
                await start.Task.WaitAsync(ct);
                store.Update(notes => notes.Add(new Note
                {
                    Id = "aaa11111",
                    OwnerUserId = 1,
                    Text = "one",
                    RemindAtUtc = DateTime.UtcNow.AddHours(1)
                }));
            }, ct);

            var t2 = Task.Run(async () =>
            {
                await start.Task.WaitAsync(ct);
                store.Update(notes => notes.Add(new Note
                {
                    Id = "bbb22222",
                    OwnerUserId = 2,
                    Text = "two",
                    RemindAtUtc = DateTime.UtcNow.AddHours(1)
                }));
            }, ct);

            start.SetResult(); // release both workers simultaneously
            await Task.WhenAll(t1, t2);

            var loaded = store.Load();
            Assert.Equal(2, loaded.Count);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static Note MakeNote(string id) => new()
    {
        Id = id,
        OwnerUserId = 1,
        Text = "",
        RemindAtUtc = DateTime.UtcNow
    };
}