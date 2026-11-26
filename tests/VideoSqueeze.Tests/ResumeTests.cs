using System.Globalization;
using System.Runtime.CompilerServices;
using Xunit;

namespace VideoSqueeze.Tests;

static class TestSetup
{
    // Never touch the real list of unfinished encodes in %LOCALAPPDATA%.
    [ModuleInitializer]
    internal static void Init() => ResumeStore.FilePath = Path.Combine(Clips.Dir, "resume.json");
}

/// <summary>Closing the app (or the PC stopping) mid-encode, then carrying on next time.</summary>
public class ResumeTests
{
    static Squeezer Make(string outDir, string container, List<string> log, Action<string, double>? progress = null, bool keep = true) =>
        new(new Settings
        {
            Mode = Mode.Manual, Codec = Codec.X265, Speed = Speed.Fast, ManualQuality = 24, Container = container,
            OutputFolder = outDir, DiscardIfLarger = false,
        }, msg => { lock (log) log.Add(msg); }, progress ?? ((_, _) => { })) { KeepUnfinished = () => keep };

    /// <summary>Starts an encode and stops it (like closing the app) once it is <paramref name="at"/> of the way through.</summary>
    static async Task InterruptAsync(string src, string outDir, string container, double at, bool keep = true)
    {
        using var cts = new CancellationTokenSource();
        var log = new List<string>();
        var sq = Make(outDir, container, log, (phase, frac) => { if (phase.StartsWith("Encoding") && frac >= at) cts.Cancel(); }, keep);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sq.RunAsync(src, cts.Token));
    }

    static async Task<List<double>> FrameTimesAsync(string file)
    {
        var r = await Tools.RunAsync(Tools.Ffprobe, new[] { "-v", "error", "-select_streams", "v:0", "-show_entries", "packet=pts_time", "-of", "csv=p=0", file },
            default, collectStdout: true);
        return r.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => double.Parse(x, CultureInfo.InvariantCulture)).OrderBy(x => x).ToList();
    }

    /// <summary>Every frame exactly once, evenly spaced, and the whole file decodes without errors.</summary>
    static async Task AssertSeamlessAsync(string output, double seconds, double fps)
    {
        var t = await FrameTimesAsync(output);
        Assert.Equal((int)Math.Round(seconds * fps), t.Count);
        var gaps = t.Zip(t.Skip(1), (a, b) => b - a).ToList();
        Assert.All(gaps, g => Assert.InRange(g, 0.5 / fps, 1.5 / fps));
        var decode = await Tools.RunAsync(Tools.Ffmpeg, new[] { "-hide_banner", "-nostdin", "-v", "error", "-i", output, "-f", "null", "-" }, default);
        Assert.Equal("", decode.Stderr.Trim());
    }

    [FfmpegFact]
    public async Task InterlacedDvd_StoppedHalfway_CarriesOnSeamlessly()
    {
        var src = await Clips.LongDvd;
        var outDir = Clips.OutDir();
        await InterruptAsync(src, outDir, "mkv", 0.6);
        Assert.NotNull(ResumeStore.Get(src));
        Assert.Single(Directory.GetFiles(outDir, "*.part"));

        var log = new List<string>();
        var r = await Make(outDir, "mkv", log).RunAsync(src, default);
        Assert.True(r.Saved, r.Status);
        Assert.Contains(log, l => l.Contains("Carrying on from last time"));
        Assert.Contains(log, l => l.Contains("Kept the first"));
        await AssertSeamlessAsync(r.OutputPath!, 60, 50); // deinterlaced to 50 fps
        Assert.Contains("codec_type=audio", await Clips.ProbeAsync(r.OutputPath!, "stream=codec_type", "a"));
        Assert.Contains("codec_type=subtitle", await Clips.ProbeAsync(r.OutputPath!, "stream=codec_type", "s"));
        Assert.Null(ResumeStore.Get(src));
        Assert.Empty(Directory.GetFiles(outDir, "*.part*"));
    }

    [FfmpegFact]
    public async Task StoppedTwice_KeepsAllEarlierProgress()
    {
        var src = await Clips.LongDvd;
        var outDir = Clips.OutDir();
        await InterruptAsync(src, outDir, "mkv", 0.45);
        await InterruptAsync(src, outDir, "mkv", 0.8);
        Assert.Single(ResumeStore.Get(src)!.Segments); // from the first stop; the second one's progress is in Current

        var log = new List<string>();
        var r = await Make(outDir, "mkv", log).RunAsync(src, default);
        Assert.True(r.Saved, r.Status);
        // "Kept the first 0:00:41 already encoded": the work from both stops, not just the first.
        var kept = log.Single(l => l.Contains("Kept the first"));
        Assert.True(TimeSpan.Parse(kept.Split("Kept the first ")[1].Split(' ')[0], CultureInfo.InvariantCulture).TotalSeconds >= 30, kept);
        await AssertSeamlessAsync(r.OutputPath!, 60, 50);
    }

    [FfmpegFact]
    public async Task Mp4_StoppedHalfway_CarriesOn()
    {
        var src = await Clips.LongDvd;
        var outDir = Clips.OutDir();
        await InterruptAsync(src, outDir, "mp4", 0.6);
        var r = await Make(outDir, "mp4", new()).RunAsync(src, default);
        Assert.True(r.Saved, r.Status);
        Assert.EndsWith(".mp4", r.OutputPath);
        await AssertSeamlessAsync(r.OutputPath!, 60, 50);
        Assert.Contains("codec_tag_string=hvc1", await Clips.ProbeAsync(r.OutputPath!, "stream=codec_tag_string"));
    }

    [FfmpegFact]
    public async Task Stop_DeletesTheUnfinishedEncode()
    {
        var src = await Clips.LongDvd;
        var outDir = Clips.OutDir();
        await InterruptAsync(src, outDir, "mkv", 0.3, keep: false);
        Assert.Null(ResumeStore.Get(src));
        Assert.Empty(Directory.GetFiles(outDir));
    }

    [FfmpegFact]
    public async Task DifferentSettings_StartsAgain()
    {
        var src = await Clips.LongDvd;
        var outDir = Clips.OutDir();
        await InterruptAsync(src, outDir, "mkv", 0.5);
        var log = new List<string>();
        var sq = new Squeezer(new Settings
        {
            Mode = Mode.Manual, Codec = Codec.X265, Speed = Speed.Fast, ManualQuality = 28, OutputFolder = outDir, DiscardIfLarger = false,
        }, msg => log.Add(msg), (_, _) => { });
        var r = await sq.RunAsync(src, default);
        Assert.True(r.Saved, r.Status);
        Assert.Contains(log, l => l.Contains("used different settings"));
        Assert.DoesNotContain(log, l => l.Contains("Kept the first"));
        await AssertSeamlessAsync(r.OutputPath!, 60, 50);
        Assert.Empty(Directory.GetFiles(outDir, "*.part*"));
    }
}

/// <summary>The join only works if the encoder writes identical stream headers each time; check the other encoders.</summary>
public class ResumeCodecTests
{
    static async Task ResumeWithAsync(Codec codec)
    {
        var src = await Clips.LongDvd;
        var outDir = Clips.OutDir();
        Squeezer Make(List<string> log, Action<string, double> progress) => new(new Settings
        {
            Mode = Mode.Manual, Codec = codec, Speed = Speed.Fast, ManualQuality = Settings.QualityRange(codec).Default,
            OutputFolder = outDir, DiscardIfLarger = false,
        }, msg => { lock (log) log.Add(msg); }, progress) { KeepUnfinished = () => true };

        using (var cts = new CancellationTokenSource())
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                Make(new(), (phase, frac) => { if (phase.StartsWith("Encoding") && frac >= 0.6) cts.Cancel(); }).RunAsync(src, cts.Token));
        var log = new List<string>();
        var r = await Make(log, (_, _) => { }).RunAsync(src, default);
        Assert.True(r.Saved, r.Status + "\n" + string.Join("\n", log));
        Assert.Contains(log, l => l.Contains("Kept the first"));
        var decode = await Tools.RunAsync(Tools.Ffmpeg, new[] { "-hide_banner", "-nostdin", "-v", "error", "-i", r.OutputPath!, "-f", "null", "-" }, default);
        Assert.Equal("", decode.Stderr.Trim());
        var count = await Tools.RunAsync(Tools.Ffprobe, new[] { "-v", "error", "-count_packets", "-select_streams", "v:0", "-show_entries", "stream=nb_read_packets", "-of", "csv=p=0", r.OutputPath! },
            default, collectStdout: true);
        Assert.Equal("3000", count.Stdout.Trim()); // 60 s deinterlaced to 50 fps, every frame once
    }

    [NvencFact]
    public Task Nvenc() => ResumeWithAsync(Codec.Nvenc);

    [FfmpegFact]
    public Task Av1() => ResumeWithAsync(Codec.Av1);
}

public class QueueFileTests
{
    [Fact] // the app saves estimates in queue.json so a carried-over list doesn't search the quality again
    public void Estimate_SurvivesJson()
    {
        var e = new Estimate(27, 95.3, 1_000_000, 4_000_000);
        Assert.Equal(e, System.Text.Json.JsonSerializer.Deserialize<Estimate>(System.Text.Json.JsonSerializer.Serialize(e)));
    }
}
