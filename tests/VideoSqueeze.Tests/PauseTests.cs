using Xunit;

namespace VideoSqueeze.Tests;

[CollectionDefinition(nameof(PauseTests), DisableParallelization = true)]
public class PauseCollection { }

/// <summary>Pause is global (it freezes every running ffmpeg), so these run on their own.</summary>
[Collection(nameof(PauseTests))]
public class PauseTests
{
    // A slow-ish encode that reports progress, written nowhere.
    static readonly string[] SlowEncode =
    {
        "-hide_banner", "-nostdin", "-loglevel", "error", "-f", "lavfi", "-i", "testsrc2=size=1280x720:rate=25:duration=60",
        "-c:v", "libx265", "-preset", "medium", "-x265-params", "log-level=none", "-progress", "pipe:1", "-nostats", "-f", "null", "-",
    };

    [FfmpegFact]
    public async Task Pause_FreezesTheEncode_AndResumeFinishesIt()
    {
        long lastUs = 0;
        var firstProgress = new TaskCompletionSource();
        var run = Tools.FfmpegAsync(SlowEncode, default, line =>
        {
            if (!line.StartsWith("out_time_us=") || !long.TryParse(line.AsSpan(12), out var us) || us <= 0) return;
            Interlocked.Exchange(ref lastUs, us);
            firstProgress.TrySetResult();
        });
        try
        {
            await firstProgress.Task.WaitAsync(TimeSpan.FromSeconds(60));
            var clock = Tools.ActiveTime;
            Tools.SetPaused(true);
            await Task.Delay(1000); // let progress already in the pipe drain
            long frozenAt = Interlocked.Read(ref lastUs);
            await Task.Delay(3000);
            Assert.Equal(frozenAt, Interlocked.Read(ref lastUs));
            Assert.False(run.IsCompleted);
            Assert.True(Tools.ActiveTime - clock < TimeSpan.FromSeconds(1), "the ETA clock should stand still while paused");
        }
        finally { Tools.SetPaused(false); }
        await run.WaitAsync(TimeSpan.FromMinutes(5));
        Assert.True(Interlocked.Read(ref lastUs) > 50_000_000);
    }

    [FfmpegFact]
    public async Task Stop_WhilePaused_EndsTheRun()
    {
        using var cts = new CancellationTokenSource();
        var started = new TaskCompletionSource();
        var run = Tools.FfmpegAsync(SlowEncode, cts.Token, _ => started.TrySetResult());
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(60));
            Tools.SetPaused(true);
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(15)));
        }
        finally { Tools.SetPaused(false); }
    }

    [FfmpegFact]
    public async Task ProcessesStartedWhilePaused_WaitForResume()
    {
        Tools.SetPaused(true);
        try
        {
            var probe = Tools.FfmpegAsync(new[] { "-hide_banner", "-version" }, default);
            await Task.Delay(1500);
            Assert.False(probe.IsCompleted);
            Tools.SetPaused(false);
            await probe.WaitAsync(TimeSpan.FromSeconds(15));
        }
        finally { Tools.SetPaused(false); }
    }
}
