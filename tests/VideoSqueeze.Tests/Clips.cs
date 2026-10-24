using System.Collections.Concurrent;
using Xunit;

// Encodes are CPU-heavy; running them side by side only makes each one slower.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace VideoSqueeze.Tests;

/// <summary>Small generated test videos, made once per test run in a temp folder.</summary>
static class Clips
{
    public static readonly bool HaveFfmpeg = Tools.Locate();
    static readonly Lazy<bool> haveNvenc = new(() => HaveFfmpeg && Tools.FfmpegAsync(new[]
    {
        "-hide_banner", "-nostdin", "-loglevel", "error", "-f", "lavfi", "-i", "color=size=256x256:duration=0.1", "-c:v", "hevc_nvenc", "-f", "null", "-",
    }, default).ContinueWith(t => t.IsCompletedSuccessfully).Result);
    public static bool HaveNvenc => haveNvenc.Value;

    public static readonly string Dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VideoSqueeze.Tests", Guid.NewGuid().ToString("N"))).FullName;
    static readonly ConcurrentDictionary<string, Lazy<Task<string>>> made = new();

    /// <summary>Real interlaced PAL video: 50 different moments per second, woven into 25 fps.</summary>
    public static Task<string> Interlaced => Make("interlaced.mkv",
        "-f", "lavfi", "-i", "testsrc2=size=720x576:rate=50", "-t", "4",
        "-vf", "tinterlace=interleave_top,setfield=tff", "-c:v", "mpeg2video", "-flags", "+ilme+ildct", "-q:v", "2");

    /// <summary>Film on a PAL DVD: progressive pictures, but flagged and coded as interlaced.</summary>
    public static Task<string> FilmFlaggedInterlaced => Make("film.mkv",
        "-f", "lavfi", "-i", "testsrc=size=720x576:rate=25", "-t", "4",
        "-vf", "setfield=tff", "-c:v", "mpeg2video", "-flags", "+ilme+ildct", "-q:v", "2");

    /// <summary>A subtitle track that first appears 15 s in — made the muxer write audio long before the first video frame.</summary>
    public static Task<string> SparseSubtitles => Make("sparse.mkv", async () =>
    {
        var srt = Path.Combine(Dir, "late.srt");
        await File.WriteAllTextAsync(srt, "1\n00:00:15,000 --> 00:00:17,000\nHello\n");
        return new[]
        {
            "-f", "lavfi", "-i", "testsrc2=size=720x576:rate=25", "-f", "lavfi", "-i", "sine=f=440:sample_rate=48000", "-i", srt,
            "-t", "20", "-map", "0", "-map", "1", "-map", "2", "-c:v", "mpeg2video", "-b:v", "6M", "-c:a", "ac3", "-c:s", "srt",
        };
    });

    /// <summary>HDR10 with mastering display and content light level info (carried in the HEVC stream only).</summary>
    public static Task<string> Hdr10 => Make("hdr10.mkv",
        "-f", "lavfi", "-i", "testsrc2=size=640x360:rate=24", "-t", "2", "-pix_fmt", "yuv420p10le", "-c:v", "libx265",
        "-x265-params", "log-level=error:hdr10=1:colorprim=bt2020:transfer=smpte2084:colormatrix=bt2020nc:" +
                        "master-display=G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(10000000,1):max-cll=1000,400",
        "-color_primaries", "bt2020", "-color_trc", "smpte2084", "-colorspace", "bt2020nc");

    static Task<string> Make(string name, params string[] args) => Make(name, () => Task.FromResult(args));

    static Task<string> Make(string name, Func<Task<string[]>> args) => made.GetOrAdd(name, _ => new Lazy<Task<string>>(async () =>
    {
        var path = Path.Combine(Dir, name);
        var all = new List<string> { "-hide_banner", "-nostdin", "-loglevel", "error", "-y" };
        all.AddRange(await args());
        all.Add(path);
        await Tools.FfmpegAsync(all, default);
        return path;
    })).Value;

    /// <summary>A fresh, empty output folder for one test.</summary>
    public static string OutDir() => Directory.CreateDirectory(Path.Combine(Dir, "out-" + Guid.NewGuid().ToString("N")[..8])).FullName;

    public static async Task<string> ProbeAsync(string file, string entries, string streams = "v:0")
    {
        var r = await Tools.RunAsync(Tools.Ffprobe, new[] { "-v", "error", "-select_streams", streams, "-show_entries", entries, "-of", "default=nw=1", file },
            default, collectStdout: true);
        return r.Stdout;
    }
}

/// <summary>A test that needs FFmpeg; skipped (not failed) on machines without it.</summary>
public sealed class FfmpegFactAttribute : FactAttribute
{
    public FfmpegFactAttribute() { if (!Clips.HaveFfmpeg) Skip = "FFmpeg not found"; }
}

/// <summary>A test that needs FFmpeg's NVIDIA encoder.</summary>
public sealed class NvencFactAttribute : FactAttribute
{
    public NvencFactAttribute() { if (!Clips.HaveNvenc) Skip = "No NVIDIA encoder available"; }
}
