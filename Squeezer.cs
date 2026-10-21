using System.Globalization;
using System.Text.RegularExpressions;

namespace VideoSqueeze;

public enum Mode { Auto, Manual, Lossless }
public enum Codec { Av1, X265, Nvenc }
public enum Speed { Fast, Balanced, Best }

public record Preset(string Name, string Description, Mode Mode, Codec Codec, Speed Speed, double TargetVmaf);

public class Settings
{
    public static readonly Preset[] Presets =
    {
        new("Fast — NVIDIA GPU (recommended)",
            "Looks identical to the original. ≈25 min per 2-hour 1080p movie.", Mode.Auto, Codec.Nvenc, Speed.Balanced, 95),
        new("Smallest files — AV1 (run overnight)",
            "Looks identical, typically 20–40% smaller than the GPU preset. ≈3–4 h per 2-hour movie.", Mode.Auto, Codec.Av1, Speed.Balanced, 95),
        new("Plays everywhere — HEVC",
            "Looks identical, best for older TVs and phones. ≈3 h per 2-hour movie.", Mode.Auto, Codec.X265, Speed.Balanced, 95),
    };

    /// <summary>Index into <see cref="Presets"/>; Presets.Length means "Custom".</summary>
    public int Preset { get; set; } = 0;
    public Mode Mode { get; set; } = Mode.Auto;
    public Codec Codec { get; set; } = Codec.Av1;
    public Speed Speed { get; set; } = Speed.Balanced;
    public double TargetVmaf { get; set; } = 95;
    public int ManualQuality { get; set; } = 30;
    public string Container { get; set; } = "mkv";
    public string OutputFolder { get; set; } = "";
    public bool DiscardIfLarger { get; set; } = true;
    public bool NotifyWhenDone { get; set; } = true;
    public bool CompareWhenDone { get; set; }

    /// <summary>(best quality, worst quality, sensible default) for the codec's CRF/CQ scale.</summary>
    public static (int Min, int Max, int Default) QualityRange(Codec c) => c switch
    {
        Codec.Av1 => (10, 55, 30),
        Codec.X265 => (10, 34, 20),
        _ => (10, 36, 24),
    };
}

public record SqueezeResult(bool Saved, string Status, long OutputSize = 0, string? OutputPath = null);

/// <summary>Result of the quality search alone, without the full encode.</summary>
public record Estimate(int Quality, double Vmaf, long PredictedSize, long OriginalSize)
{
    public double SavedPercent => 100.0 * (1 - (double)PredictedSize / OriginalSize);
    public bool WorthIt => PredictedSize < OriginalSize * 0.95;
}

/// <summary>Compresses one file according to the settings.</summary>
class Squeezer
{
    const double SampleSeconds = 5;
    const string CompareFormat = "yuv420p10le";

    static readonly HashSet<string> X265LosslessPixFmts = new()
    {
        "yuv420p", "yuv422p", "yuv444p", "yuvj420p", "yuvj422p", "yuvj444p",
        "yuv420p10le", "yuv422p10le", "yuv444p10le", "yuv420p12le", "yuv422p12le", "yuv444p12le",
        "gray", "gray10le", "gray12le",
    };
    static readonly HashSet<string> Mp4Audio = new() { "aac", "mp3", "ac3", "eac3", "opus", "flac", "alac" };
    static readonly HashSet<string> TextSubs = new() { "subrip", "ass", "ssa", "mov_text", "webvtt", "text" };

    readonly Settings s;
    readonly Action<string> log;
    readonly Action<string, double> progress;
    string? vf; // deinterlace / field filter for the current file, see DecideScanAsync

    public Squeezer(Settings settings, Action<string> log, Action<string, double> progress)
    {
        s = settings;
        this.log = log;
        this.progress = progress;
    }

    /// <summary>Runs only the automatic quality search and predicts the output size.</summary>
    public async Task<Estimate> EstimateAsync(string src, CancellationToken ct)
    {
        progress("Analyzing", 0);
        var m = await MediaInfo.ProbeAsync(src, ct);
        log($"▶ Estimating {Path.GetFileName(src)} — {m.Describe()}");
        await DecideScanAsync(m, ct);
        var (q, vmaf, predicted) = await SearchQualityAsync(m, ct);
        var e = new Estimate(q, vmaf, predicted, m.Size);
        log($"  Estimate: {Format.Bytes(m.Size)} → ≈{Format.Bytes(predicted)} " +
            (e.WorthIt ? $"(≈{e.SavedPercent:0}% smaller) at quality {q}, VMAF ≈ {vmaf:0.0}" : "— no worthwhile saving, will be skipped"));
        return e;
    }

    /// <param name="known">A previous <see cref="EstimateAsync"/> result for the same settings, to skip the search.</param>
    public async Task<SqueezeResult> RunAsync(string src, CancellationToken ct, Estimate? known = null)
    {
        progress("Analyzing", 0);
        var m = await MediaInfo.ProbeAsync(src, ct);
        log($"▶ {Path.GetFileName(src)} — {m.Describe()}");

        bool ffv1 = s.Mode == Mode.Lossless && !X265LosslessPixFmts.Contains(m.PixFmt);
        string container = ffv1 ? "mkv" : s.Container;
        if (ffv1 && s.Container != "mkv") log("  Lossless FFV1 needs MKV; writing .mkv.");
        await DecideScanAsync(m, ct);

        int quality = 0;
        if (s.Mode == Mode.Auto)
        {
            var (q, vmaf, predicted) = known != null && known.OriginalSize == m.Size
                ? (known.Quality, known.Vmaf, known.PredictedSize)
                : await SearchQualityAsync(m, ct);
            quality = q;
            log($"  {(known != null ? "Using earlier estimate: " : "")}quality {q} (VMAF ≈ {vmaf:0.0}); " +
                $"predicted size ≈ {Format.Bytes(predicted)} ({100.0 * predicted / m.Size:0}% of original)");
            if (s.DiscardIfLarger && predicted >= m.Size * 0.95)
            {
                log("  Skipped: this file is already efficiently compressed — re-encoding would save < 5%.");
                return new SqueezeResult(false, "Skipped (already efficient)");
            }
        }
        else if (s.Mode == Mode.Manual)
            quality = s.ManualQuality;

        var outPath = OutputPath(src, container);
        var partPath = outPath + ".part";
        try
        {
            var args = new List<string> { "-hide_banner", "-nostdin", "-y", "-loglevel", "error", "-i", src, "-map", $"0:{m.VideoIndex}" };
            args.AddRange(s.Mode == Mode.Lossless ? LosslessArgs(m, ffv1) : VideoArgs(m, quality, container));
            if (vf != null) args.AddRange(new[] { "-vf", vf });
            args.AddRange(OtherStreamArgs(m, container));
            args.AddRange(new[] { "-map_metadata", "0", "-map_chapters", "0", "-max_muxing_queue_size", "4096" });
            // Sparse tracks (e.g. DVD subtitles that first appear minutes in) otherwise make the muxer write seconds of
            // audio ahead of the first video frame; VLC then drops the opening keyframe → missing/blocky first seconds.
            // Wait up to 120 s for every track instead of the default 10 s.
            args.AddRange(new[] { "-max_interleave_delta", "120000000" });
            if (container == "mp4") args.AddRange(new[] { "-movflags", "+faststart" });
            args.AddRange(new[] { "-progress", "pipe:1", "-nostats", "-f", container == "mp4" ? "mp4" : "matroska", partPath });

            var started = DateTime.UtcNow;
            await Tools.FfmpegAsync(args, ct, line =>
            {
                if (!line.StartsWith("out_time_us=") || m.Duration <= 0) return;
                if (!long.TryParse(line.AsSpan(12), out var us) || us < 0) return;
                double frac = Math.Clamp(us / 1e6 / m.Duration, 0, 1);
                var elapsed = DateTime.UtcNow - started;
                string eta = frac > 0.01 ? $" — {TimeSpan.FromSeconds(elapsed.TotalSeconds / frac * (1 - frac)):hh\\:mm\\:ss} left" : "";
                progress($"Encoding {frac:P0}{eta}", frac);
            });

            await CheckVideoStartsFirstAsync(partPath, ct);
            long outSize = new FileInfo(partPath).Length;
            if (s.DiscardIfLarger && outSize >= m.Size)
            {
                File.Delete(partPath);
                log($"  Output ({Format.Bytes(outSize)}) was not smaller than the original — discarded, original kept.");
                return new SqueezeResult(false, "Skipped (not smaller)");
            }
            File.Move(partPath, outPath);
            double saved = 100.0 * (1 - (double)outSize / m.Size);
            string change = saved >= 0 ? $"{saved:0.#}% smaller" : $"{-saved:0.#}% LARGER";
            log($"  ✔ {Format.Bytes(m.Size)} → {Format.Bytes(outSize)} ({change}) → {outPath}");
            return new SqueezeResult(true, saved >= 0 ? $"Done (−{saved:0}%)" : $"Done (+{-saved:0}%)", outSize, outPath);
        }
        finally
        {
            try { if (File.Exists(partPath)) File.Delete(partPath); } catch { }
        }
    }

    /// <summary>Warns if the first video frame sits behind lots of other packets — players like VLC then show a broken start.</summary>
    async Task CheckVideoStartsFirstAsync(string file, CancellationToken ct)
    {
        var r = await Tools.RunAsync(Tools.Ffprobe, new[]
        {
            "-v", "error", "-show_entries", "packet=stream_index,codec_type", "-of", "csv=p=0", "-read_intervals", "%+#200", file,
        }, ct, collectStdout: true);
        var lines = r.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int firstVideo = Array.FindIndex(lines, l => l.StartsWith("0"));
        if (firstVideo > 40)
            log($"  ⚠ Warning: the video starts {firstVideo} packets into the file; some players may show a broken first few seconds.");
    }

    static readonly Regex IdetCounts = new(@"Multi frame detection: TFF:\s*(\d+)\s+BFF:\s*(\d+)\s+Progressive:\s*(\d+)", RegexOptions.Compiled);

    /// <summary>
    /// AV1 can't signal interlacing (and x265 doesn't by default), so an interlaced source encoded as-is plays without
    /// deinterlacing → combing on motion. Real interlaced video is deinterlaced to double rate (50/60 fps, keeps all
    /// motion); film that is merely flagged interlaced (most PAL DVDs) stays at its frame rate, marked progressive.
    /// </summary>
    async Task DecideScanAsync(MediaInfo m, CancellationToken ct)
    {
        vf = null;
        if (!m.FlaggedInterlaced || s.Mode == Mode.Lossless) return;
        long interlaced = 0, total = 0;
        var positions = m.Duration > 120 ? new[] { 0.25, 0.5, 0.75 }.Select(f => m.Duration * f).ToArray() : new[] { 0.0 };
        for (int i = 0; i < positions.Length; i++)
        {
            progress($"Checking interlacing {i + 1}/{positions.Length}", (double)i / positions.Length);
            var r = await Tools.FfmpegAsync(new[]
            {
                "-hide_banner", "-nostdin", "-ss", positions[i].ToString("0.###", CultureInfo.InvariantCulture), "-i", m.Path,
                "-t", "20", "-map", $"0:{m.VideoIndex}", "-vf", "idet", "-an", "-sn", "-dn", "-f", "null", "-",
            }, ct);
            foreach (Match match in IdetCounts.Matches(r.Stderr))
            {
                long tff = long.Parse(match.Groups[1].Value), bff = long.Parse(match.Groups[2].Value), prog = long.Parse(match.Groups[3].Value);
                interlaced += tff + bff;
                total += tff + bff + prog;
            }
        }
        // No verdict (unreadable samples): trust the flag.
        if (total == 0 || interlaced > total * 0.1)
        {
            vf = "bwdif=mode=send_field";
            m.Fps *= 2;
            log($"  Interlaced video ({interlaced} of {total} frames) — deinterlacing to {m.Fps:0.##} fps (keeps all motion).");
        }
        else
        {
            vf = "setfield=prog";
            log($"  Flagged interlaced but the picture is progressive ({total - interlaced} of {total} frames) — kept at {m.Fps:0.##} fps.");
        }
    }

    // ---------- automatic quality search ----------

    /// <summary>
    /// Encodes short samples at different quality levels and measures each one against the original with VMAF,
    /// then returns the most compressed setting that still reaches the target score.
    /// </summary>
    async Task<(int Quality, double Vmaf, long PredictedSize)> SearchQualityAsync(MediaInfo m, CancellationToken ct)
    {
        var (qMin, qMax, qDefault) = Settings.QualityRange(s.Codec);
        var tmp = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VideoSqueeze", Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            // Pick sample positions spread through the video.
            var starts = new List<double>();
            double len = SampleSeconds;
            if (m.Duration < SampleSeconds * 4)
            {
                starts.Add(0);
                len = m.Duration > 0 ? m.Duration : 20;
            }
            else
            {
                int n = Math.Clamp((int)Math.Round(m.Duration / 300), 3, 8);
                for (int i = 0; i < n; i++) starts.Add(m.Duration * (i + 1) / (n + 1) - len / 2);
            }

            // Lossless reference clips (frame-exact, so encoded samples line up with them for VMAF).
            var refs = new List<string>();
            for (int i = 0; i < starts.Count; i++)
            {
                progress($"Preparing samples {i + 1}/{starts.Count}", (double)i / starts.Count);
                var refPath = Path.Combine(tmp, $"ref{i}.mkv");
                await Tools.FfmpegAsync(new[]
                {
                    "-hide_banner", "-nostdin", "-y", "-loglevel", "error",
                    "-ss", starts[i].ToString("0.###", CultureInfo.InvariantCulture), "-i", m.Path,
                    "-t", len.ToString("0.###", CultureInfo.InvariantCulture), "-map", $"0:{m.VideoIndex}",
                    // Same deinterlacing as the real encode, so VMAF compares like with like.
                    "-vf", (vf != null ? vf + "," : "") + "setpts=PTS-STARTPTS", "-c:v", "ffv1", "-pix_fmt", CompareFormat, "-an", "-sn", "-dn", refPath,
                }, ct);
                refs.Add(refPath);
            }

            log($"  Searching for the smallest size that keeps VMAF ≥ {s.TargetVmaf} ({refs.Count} sample(s))…");
            int step = 0, maxSteps = (int)Math.Ceiling(Math.Log2(qMax - qMin + 1)) + 1;
            var results = new Dictionary<int, (double Vmaf, long Bytes)>();

            async Task<(double Vmaf, long Bytes)> Evaluate(int q)
            {
                step++;
                double vmafSum = 0;
                long bytes = 0;
                var perSample = new List<string>();
                for (int i = 0; i < refs.Count; i++)
                {
                    progress($"Finding best quality — try {step}, quality {q}, sample {i + 1}/{refs.Count}",
                        Math.Min(1, (step - 1 + (double)i / refs.Count) / maxSteps));
                    var enc = Path.Combine(tmp, $"enc{i}_{q}.mkv");
                    var args = new List<string> { "-hide_banner", "-nostdin", "-y", "-loglevel", "error", "-i", refs[i] };
                    args.AddRange(VideoArgs(m, q, "mkv"));
                    args.AddRange(new[] { "-an", "-f", "matroska", enc });
                    await Tools.FfmpegAsync(args, ct);
                    bytes += new FileInfo(enc).Length;
                    double v = await VmafAsync(enc, refs[i], m.Height > 1440, ct);
                    vmafSum += v;
                    perSample.Add(v.ToString("0.0", CultureInfo.InvariantCulture));
                    File.Delete(enc);
                }
                var r = (vmafSum / refs.Count, bytes);
                results[q] = r;
                log($"    quality {q}: VMAF {r.Item1:0.00}" + (refs.Count > 1 ? $"  (samples: {string.Join(", ", perSample)})" : ""));
                return r;
            }

            // Binary search for the highest (= most compressed) quality value that still meets the target.
            int lo = qMin, hi = qMax;
            int? best = null;
            bool first = true;
            while (lo <= hi)
            {
                int mid = first ? Math.Clamp(qDefault, lo, hi) : (lo + hi + 1) / 2;
                first = false;
                var (vmaf, _) = await Evaluate(mid);
                if (vmaf >= s.TargetVmaf) { best = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            if (best == null)
            {
                best = qMin;
                log($"  Target not reachable even at the highest quality — using quality {qMin}.");
            }

            var chosen = results[best.Value];
            double sampledSeconds = len * refs.Count;
            long predicted = (long)(chosen.Bytes / sampledSeconds * Math.Max(m.Duration, len)) + m.AudioBytes;
            return (best.Value, chosen.Vmaf, predicted);
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }

    static readonly Regex VmafScore = new(@"VMAF score[:=]\s*([0-9.]+)", RegexOptions.Compiled);

    /// <param name="start">Optional: measure only <paramref name="length"/> seconds from this point of both files.</param>
    /// <param name="refFilter">Optional filter for the reference, e.g. the deinterlacer the encode used.</param>
    internal static async Task<double> VmafAsync(string distorted, string reference, bool uhd, CancellationToken ct,
        double? start = null, double length = 1, string? refFilter = null)
    {
        // The 4K model is calibrated for UHD viewing; the default model is for 1080p.
        string model = uhd ? "vmaf_4k_v0.6.1" : "vmaf_v0.6.1";
        // Pair frames by index, not timestamp: container timestamp rounding can otherwise shift one input by a frame,
        // which makes VMAF compare neighbouring frames and wildly under-report quality.
        string graph = $"[0:v]format={CompareFormat},settb=AVTB,setpts=N[d];" +
                       $"[1:v]{(refFilter != null ? refFilter + "," : "")}format={CompareFormat},settb=AVTB,setpts=N[r];" +
                       $"[d][r]libvmaf=model=version={model}:n_threads={Environment.ProcessorCount}";
        var window = start is double s
            ? new[] { "-ss", s.ToString("0.###", CultureInfo.InvariantCulture), "-t", length.ToString("0.###", CultureInfo.InvariantCulture) }
            : Array.Empty<string>();
        var args = new List<string> { "-hide_banner", "-nostdin" };
        args.AddRange(window);
        args.AddRange(new[] { "-i", distorted });
        args.AddRange(window);
        args.AddRange(new[] { "-i", reference, "-lavfi", graph, "-f", "null", "-" });
        var r = await Tools.FfmpegAsync(args, ct);
        var match = VmafScore.Match(r.Stderr);
        if (!match.Success) throw new Exception("Couldn't read VMAF score:\r\n" + Tools.Tail(r.Stderr, 5));
        return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    // ---------- ffmpeg argument building ----------

    List<string> VideoArgs(MediaInfo m, int q, string container)
    {
        string qs = q.ToString(CultureInfo.InvariantCulture);
        int gop = Math.Max(24, (int)Math.Round(m.Fps * 10));
        var a = new List<string>();
        switch (s.Codec)
        {
            case Codec.Av1:
                a.AddRange(new[]
                {
                    "-c:v", "libsvtav1", "-crf", qs,
                    "-preset", s.Speed switch { Speed.Fast => "8", Speed.Balanced => "6", _ => "4" },
                    "-g", gop.ToString(), "-pix_fmt", "yuv420p10le",
                });
                break;
            case Codec.X265:
                a.AddRange(new[]
                {
                    "-c:v", "libx265", "-crf", qs,
                    "-preset", s.Speed switch { Speed.Fast => "fast", Speed.Balanced => "slow", _ => "slower" },
                    "-pix_fmt", "yuv420p10le", "-x265-params", "log-level=error",
                });
                if (container == "mp4") a.AddRange(new[] { "-tag:v", "hvc1" });
                break;
            case Codec.Nvenc:
                a.AddRange(new[]
                {
                    "-c:v", "hevc_nvenc", "-preset", s.Speed == Speed.Fast ? "p5" : "p7", "-tune", "hq",
                    // Without an explicit high maxrate NVENC silently caps the bitrate and ignores -cq on detailed footage.
                    "-rc", "vbr", "-cq", qs, "-b:v", "0", "-maxrate", "500M", "-bufsize", "1000M",
                    "-spatial-aq", "1", "-temporal-aq", "1",
                    "-rc-lookahead", "32", "-pix_fmt", "p010le", "-profile:v", "main10",
                });
                if (s.Speed == Speed.Best) a.AddRange(new[] { "-multipass", "fullres" });
                if (container == "mp4") a.AddRange(new[] { "-tag:v", "hvc1" });
                break;
        }
        a.AddRange(ColorArgs(m));
        return a;
    }

    List<string> LosslessArgs(MediaInfo m, bool ffv1)
    {
        var a = ffv1
            ? new List<string> { "-c:v", "ffv1", "-level", "3", "-g", "1", "-slices", "16", "-slicecrc", "1" }
            : new List<string> { "-c:v", "libx265", "-preset", "medium", "-x265-params", "lossless=1:log-level=error", "-pix_fmt", m.PixFmt };
        if (!ffv1 && s.Container == "mp4") a.AddRange(new[] { "-tag:v", "hvc1" });
        a.AddRange(ColorArgs(m));
        return a;
    }

    static IEnumerable<string> ColorArgs(MediaInfo m)
    {
        if (m.Primaries != null) { yield return "-color_primaries"; yield return m.Primaries; }
        if (m.Transfer != null) { yield return "-color_trc"; yield return m.Transfer; }
        if (m.Matrix != null) { yield return "-colorspace"; yield return m.Matrix; }
        if (m.Range != null) { yield return "-color_range"; yield return m.Range; }
    }

    /// <summary>Audio, subtitles and attachments are copied untouched (never re-compressed).</summary>
    IEnumerable<string> OtherStreamArgs(MediaInfo m, string container)
    {
        var a = new List<string>();
        if (container == "mkv")
        {
            a.AddRange(new[] { "-map", "0:a?", "-map", "0:s?", "-map", "0:t?", "-c:a", "copy", "-c:s", "copy", "-c:t", "copy" });
            return a;
        }

        // MP4 can't hold every audio/subtitle format.
        a.AddRange(new[] { "-map", "0:a?" });
        for (int i = 0; i < m.Audio.Count; i++)
        {
            if (Mp4Audio.Contains(m.Audio[i].Codec))
                a.AddRange(new[] { $"-c:a:{i}", "copy" });
            else
            {
                a.AddRange(new[] { $"-c:a:{i}", "flac" });
                log($"  Audio track {i + 1} ({m.Audio[i].Codec}) isn't MP4-compatible; converting to FLAC (lossless).");
            }
        }
        int sOut = 0;
        foreach (var (index, codec) in m.Subtitles)
        {
            if (TextSubs.Contains(codec))
            {
                a.AddRange(new[] { "-map", $"0:{index}", $"-c:s:{sOut++}", "mov_text" });
            }
            else log($"  Subtitle track ({codec}) can't go in MP4 and was left out — choose MKV to keep it.");
        }
        a.AddRange(new[] { "-strict", "-2" });
        return a;
    }

    string OutputPath(string src, string container)
    {
        var dir = string.IsNullOrWhiteSpace(s.OutputFolder) ? Path.GetDirectoryName(src)! : s.OutputFolder;
        Directory.CreateDirectory(dir);
        var name = Path.GetFileNameWithoutExtension(src) + "_squeezed";
        var path = Path.Combine(dir, $"{name}.{container}");
        for (int i = 2; File.Exists(path); i++) path = Path.Combine(dir, $"{name} ({i}).{container}");
        return path;
    }
}
