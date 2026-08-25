using Serilog;
using YtMusicController.App.Diagnostics;

namespace YtMusicController.Tests;

public sealed class DebugLogStoreTests
{
    [Fact]
    public void Sink_redacts_bearer_tokens_and_keeps_recent_entries()
    {
        var store = new DebugLogStore();
        using var logger = new LoggerConfiguration().WriteTo.Sink(store).CreateLogger();

        logger.Information("Authorization: Bearer abcdefghijklmnopqrstuvwxyz123456");
        for (var index = 0; index < DebugLogStore.Capacity + 5; index++)
            logger.Information("Entry {Index}", index);

        var text = store.GetSnapshotText();
        Assert.DoesNotContain("abcdefghijklmnopqrstuvwxyz123456", text);
        Assert.DoesNotContain("Entry 0", text);
        Assert.Contains($"Entry {DebugLogStore.Capacity + 4}", text);
        Assert.Equal(DebugLogStore.Capacity, store.Count);
    }

    [Fact]
    public void Clear_removes_buffered_entries()
    {
        var store = new DebugLogStore();
        using var logger = new LoggerConfiguration().WriteTo.Sink(store).CreateLogger();
        logger.Warning("Test warning");

        store.Clear();

        Assert.Equal(0, store.Count);
        Assert.Equal(string.Empty, store.GetSnapshotText());
    }
}
