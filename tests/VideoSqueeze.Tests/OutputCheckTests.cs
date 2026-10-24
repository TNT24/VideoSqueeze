using Xunit;

namespace VideoSqueeze.Tests;

/// <summary>The post-encode comparison of output against original (no FFmpeg needed).</summary>
public class OutputCheckTests
{
    static MediaInfo Movie(double duration = 7200, double video = 7200, int audio = 2, params string[] subs) => new()
    {
        Duration = duration,
        VideoDuration = video,
        Audio = Enumerable.Range(1, audio).Select(i => (i, "ac3", 448_000L)).ToList(),
        Subtitles = subs.Select((codec, i) => (10 + i, codec)).ToList(),
    };

    [Fact]
    public void Matching_Output_Passes() =>
        Assert.Null(Squeezer.OutputProblem(Movie(subs: "dvd_subtitle"), Movie(7201, 7199, subs: "dvd_subtitle"), "mkv"));

    [Fact]
    public void Truncated_Output_Fails() =>
        Assert.Contains("long", Squeezer.OutputProblem(Movie(), Movie(3600, 3600), "mkv"));

    [Fact]
    public void Video_Stopping_Early_Fails() =>
        Assert.Contains("picture stops", Squeezer.OutputProblem(Movie(), Movie(7200, 5400), "mkv"));

    [Fact]
    public void Missing_Audio_Track_Fails() =>
        Assert.Contains("audio", Squeezer.OutputProblem(Movie(audio: 2), Movie(audio: 1), "mkv"));

    [Fact]
    public void Missing_Subtitle_Track_Fails_In_Mkv() =>
        Assert.Contains("subtitle", Squeezer.OutputProblem(Movie(subs: "dvd_subtitle"), Movie(), "mkv"));

    [Fact]
    public void Mp4_Leaves_Out_Picture_Subtitles_On_Purpose() =>
        Assert.Null(Squeezer.OutputProblem(Movie(subs: new[] { "dvd_subtitle", "subrip" }), Movie(subs: "mov_text"), "mp4"));

    [Fact]
    public void Unknown_Track_Lengths_Are_Not_Held_Against_The_Output() =>
        Assert.Null(Squeezer.OutputProblem(Movie(video: 0), Movie(video: 0), "mkv"));
}
