using RemindMe.Core;

namespace RemindMe.Tests;

public class JsonLongSetStoreTests : IDisposable
{
    private readonly string _path;

    public JsonLongSetStoreTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"ids_{Guid.NewGuid():N}.json");
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
        var store = new JsonLongSetStore(_path);
        Assert.Empty(store.Load());
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var store = new JsonLongSetStore(_path);
        store.Save([1, 2, 3]);

        var loaded = store.Load();
        Assert.Equal(3, loaded.Count);
        Assert.Contains(1, loaded);
        Assert.Contains(2, loaded);
        Assert.Contains(3, loaded);
    }

    [Fact]
    public void Save_ReplacesExisting()
    {
        var store = new JsonLongSetStore(_path);
        store.Save(new HashSet<long> { 1, 2 });
        store.Save(new HashSet<long> { 3 });

        var loaded = store.Load();
        Assert.Single(loaded);
        Assert.Contains(3, loaded);
    }
}