using Xunit;

namespace VideoSqueeze.Tests;

/// <summary>End-to-end encodes of small generated clips, covering problems real files have hit.</summary>
public class SqueezerTests
{
    static Squeezer Manual(string outDir, Codec codec = Codec.X265) => new(new Settings
    {
        Mode = Mode.Manual, Codec = codec, Speed = Speed.Fast, ManualQuality = Settings.QualityRange(codec).Default,
        OutputFolder = outDir, DiscardIfLarger = false,
    }, _ => { }, (_, _) => { });

    static async Task<string> SqueezeAsync(string src, Codec codec = Codec.X265)
    {
        var r = await Manual(Clips.OutDir(), codec).RunAsync(src, default);
        Assert.True(r.Saved, r.Status);
        return r.OutputPath!;
    }

    [FfmpegFact]
    public async Task InterlacedVideo_IsDeinterlacedToDoubleRate()
    {
        var output = await SqueezeAsync(await Clips.Interlaced);
        var info = await Clips.ProbeAsync(output, "stream=avg_frame_rate,field_order");
        Assert.Contains("avg_frame_rate=50/1", info);
        Assert.Contains("field_order=progressive", info);
    }

    [FfmpegFact]
    public async Task FilmFlaggedInterlaced_KeepsFrameRate_MarkedProgressive()
    {
        var output = await SqueezeAsync(await Clips.FilmFlaggedInterlaced);
        var info = await Clips.ProbeAsync(output, "stream=avg_frame_rate,field_order");
        Assert.Contains("avg_frame_rate=25/1", info);
        Assert.Contains("field_order=progressive", info);
    }

    [FfmpegFact]
    public async Task InterlacedEstimate_HoldsForTheDeinterlacedEncode()
    {
        // The quality search must sample the video as it will be encoded (deinterlaced, 50 fps). Sampling the
        // interlaced original instead finds a quality that looks fine there but falls short on the real encode.
        var src = await Clips.Interlaced;
        var auto = new Squeezer(new Settings { Mode = Mode.Auto, Codec = Codec.X265, Speed = Speed.Fast, TargetVmaf = 95 }, _ => { }, (_, _) => { });
        var e = await auto.EstimateAsync(src, default);
        Assert.True(e.Vmaf >= 95, $"VMAF {e.Vmaf}");

        var manual = new Squeezer(new Settings
        {
            Mode = Mode.Manual, Codec = Codec.X265, Speed = Speed.Fast, ManualQuality = e.Quality, OutputFolder = Clips.OutDir(), DiscardIfLarger = false,
        }, _ => { }, (_, _) => { });
        var r = await manual.RunAsync(src, default);
        Assert.True(r.Saved, r.Status);
        double ratio = (double)r.OutputSize / e.PredictedSize;
        Assert.InRange(ratio, 0.75, 1.33);
        double vmaf = await Squeezer.VmafAsync(r.OutputPath!, src, false, default, refFilter: "bwdif=mode=send_field");
        Assert.True(vmaf >= 94.5, $"quality {e.Quality}: real encode VMAF {vmaf}");
    }

    [FfmpegFact]
    public async Task SparseSubtitles_VideoStillComesFirst()
    {
        // Without -max_interleave_delta the first video packet landed ~170 packets in, and VLC dropped the opening keyframe.
        var output = await SqueezeAsync(await Clips.SparseSubtitles);
        var r = await Tools.RunAsync(Tools.Ffprobe, new[] { "-v", "error", "-show_entries", "packet=stream_index", "-of", "csv=p=0", "-read_intervals", "%+#300", output },
            default, collectStdout: true);
        var packets = r.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int firstVideo = Array.IndexOf(packets, "0");
        Assert.InRange(firstVideo, 0, 5);
    }

    [FfmpegFact]
    public Task Hdr10Info_Survives_X265() => AssertHdrSurvives(Codec.X265);

    [FfmpegFact]
    public Task Hdr10Info_Survives_Av1() => AssertHdrSurvives(Codec.Av1);

    [NvencFact]
    public Task Hdr10Info_Survives_Nvenc() => AssertHdrSurvives(Codec.Nvenc);

    static async Task AssertHdrSurvives(Codec codec)
    {
        // RunAsync's own output check fails the encode if it's lost; this also checks the result directly.
        var output = await SqueezeAsync(await Clips.Hdr10, codec);
        var side = await MediaInfo.HdrSideDataAsync(output, default);
        Assert.Contains("Mastering display metadata", side);
        Assert.Contains("Content light level metadata", side);
    }
}
