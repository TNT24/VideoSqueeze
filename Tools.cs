using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace VideoSqueeze;

/// <summary>Finds ffmpeg/ffprobe and runs them.</summary>
static class Tools
{
    public static string Ffmpeg { get; private set; } = "";
    public static string Ffprobe { get; private set; } = "";

    public static bool Locate()
    {
        var dirs = new List<string> { AppContext.BaseDirectory, Path.Combine(AppContext.BaseDirectory, "ffmpeg") };
        dirs.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries));
        foreach (var root in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
                 })
            dirs.Add(Path.Combine(root, "Shutter Encoder", "app", "Library"));

        foreach (var d in dirs)
        {
            try
            {
                var dir = d.Trim().Trim('"');
                var ff = Path.Combine(dir, "ffmpeg.exe");
                var fp = Path.Combine(dir, "ffprobe.exe");
                if (File.Exists(ff) && File.Exists(fp))
                {
                    Ffmpeg = ff;
                    Ffprobe = fp;
                    return true;
                }
            }
            catch { /* malformed PATH entry */ }
        }
        return false;
    }

    public record RunResult(int ExitCode, string Stdout, string Stderr);

    // ---------- pause ----------
    // Pausing freezes the running ffmpeg/ffprobe processes in place (all their threads suspended), so an encode carries
    // on exactly where it was instead of starting over. Processes started while paused wait until resumed.

    static readonly object pauseLock = new();
    static readonly HashSet<Process> running = new();
    static TaskCompletionSource resumed = Completed();
    static readonly Stopwatch activeClock = Stopwatch.StartNew();

    static TaskCompletionSource Completed()
    {
        var t = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        t.SetResult();
        return t;
    }

    public static bool Paused { get; private set; }

    /// <summary>A clock that stands still while paused (for ETAs).</summary>
    public static TimeSpan ActiveTime => activeClock.Elapsed;

    public static void SetPaused(bool pause)
    {
        lock (pauseLock)
        {
            if (pause == Paused) return;
            Paused = pause;
            if (pause)
            {
                activeClock.Stop();
                resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            else
            {
                activeClock.Start();
                resumed.TrySetResult();
            }
            foreach (var p in running) Freeze(p, pause);
        }
    }

    static void Freeze(Process p, bool pause)
    {
        try { if (!p.HasExited) _ = pause ? NtSuspendProcess(p.Handle) : NtResumeProcess(p.Handle); } catch { }
    }

    /// <summary>Starts the process once not paused, and tracks it so a later pause freezes it too.</summary>
    static async Task StartTrackedAsync(Process p, CancellationToken ct)
    {
        while (true)
        {
            Task wait;
            lock (pauseLock)
            {
                if (!Paused)
                {
                    p.Start();
                    running.Add(p);
                    try { AssignProcessToJobObject(KillOnExitJob.Value, p.Handle); } catch { }
                    return;
                }
                wait = resumed.Task;
            }
            await wait.WaitAsync(ct);
        }
    }

    static void Untrack(Process p) { lock (pauseLock) running.Remove(p); }

    // FFmpeg goes into a job that Windows closes when VideoSqueeze exits for any reason (even a crash), ending it too.
    // Otherwise it would carry on in the background, still writing the unfinished file the next run wants to continue.
    static readonly Lazy<IntPtr> KillOnExitJob = new(() =>
    {
        var job = CreateJobObject(IntPtr.Zero, null);
        var info = new JobExtendedLimits { LimitFlags = 0x2000 }; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        SetInformationJobObject(job, 9, ref info, (uint)System.Runtime.InteropServices.Marshal.SizeOf<JobExtendedLimits>()); // JobObjectExtendedLimitInformation
        return job;
    });

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct JobExtendedLimits
    {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass, SchedulingClass;
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount;
        public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern IntPtr CreateJobObject(IntPtr attributes, string? name);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref JobExtendedLimits info, uint length);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [System.Runtime.InteropServices.DllImport("ntdll.dll")] static extern int NtSuspendProcess(IntPtr handle);
    [System.Runtime.InteropServices.DllImport("ntdll.dll")] static extern int NtResumeProcess(IntPtr handle);

    /// <summary>Runs a process; kills it (and children) if cancelled.</summary>
    public static async Task<RunResult> RunAsync(string exe, IEnumerable<string> args, CancellationToken ct,
        Action<string>? onStdoutLine = null, bool collectStdout = false)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        if (Environment.GetEnvironmentVariable("VIDEOSQUEEZE_DEBUG") == "1")
            Console.WriteLine("$ " + Path.GetFileName(exe) + " " + string.Join(" ", psi.ArgumentList.Select(a => a.Contains(' ') ? $"\"{a}\"" : a)));

        using var p = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        p.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            onStdoutLine?.Invoke(e.Data);
            if (collectStdout) lock (stdout) stdout.AppendLine(e.Data);
        };
        p.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (stderr)
            {
                stderr.AppendLine(e.Data);
                if (stderr.Length > 256_000) stderr.Remove(0, stderr.Length - 128_000);
            }
        };

        await StartTrackedAsync(p, ct);
        try
        {
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            // Keep the PC responsive while encoding.
            try { p.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }

            using (ct.Register(() => { try { if (!p.HasExited) p.Kill(true); } catch { } }))
                await p.WaitForExitAsync();
        }
        finally { Untrack(p); }
        ct.ThrowIfCancellationRequested();
        return new RunResult(p.ExitCode, stdout.ToString(), stderr.ToString());
    }

    /// <summary>Runs ffmpeg and returns its binary stdout (e.g. an image piped out with "-f image2pipe -").</summary>
    public static async Task<byte[]> FfmpegBytesAsync(IEnumerable<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(Ffmpeg)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = new Process { StartInfo = psi };
        await StartTrackedAsync(p, ct);
        var stderr = p.StandardError.ReadToEndAsync();
        using var ms = new MemoryStream();
        try
        {
            using (ct.Register(() => { try { if (!p.HasExited) p.Kill(true); } catch { } }))
            {
                await p.StandardOutput.BaseStream.CopyToAsync(ms);
                await p.WaitForExitAsync();
            }
        }
        finally { Untrack(p); }
        ct.ThrowIfCancellationRequested();
        if (p.ExitCode != 0 || ms.Length == 0) throw new Exception("FFmpeg failed:\r\n" + Tail(await stderr, 5));
        return ms.ToArray();
    }

    public static async Task<RunResult> FfmpegAsync(IEnumerable<string> args, CancellationToken ct, Action<string>? onStdoutLine = null)
    {
        var r = await RunAsync(Ffmpeg, args, ct, onStdoutLine);
        if (r.ExitCode != 0)
            throw new Exception("FFmpeg failed:\r\n" + Tail(r.Stderr, 12));
        return r;
    }

    public static string Tail(string text, int lines) =>
        string.Join("\r\n", text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).TakeLast(lines));
}

/// <summary>What ffprobe tells us about a file.</summary>
class MediaInfo
{
    public string Path = "";
    public long Size;
    public double Duration;
    public double VideoDuration; // the video track alone; 0 if the file doesn't say
    public int VideoIndex = -1;
    public string VideoCodec = "";
    public int Width, Height;
    public string PixFmt = "";
    public double Fps = 30;
    public string? Primaries, Transfer, Matrix, Range;
    public string? FieldOrder;
    public long VideoBitrate;
    public List<(int Index, string Codec, long Bitrate)> Audio = new();
    public List<(int Index, string Codec)> Subtitles = new();

    public bool IsHdr => Transfer is "smpte2084" or "arib-std-b67";

    /// <summary>The stream says it's interlaced (DVDs often do even when the picture is really progressive film).</summary>
    public bool FlaggedInterlaced => FieldOrder is "tt" or "bb" or "tb" or "bt";

    /// <summary>Estimated bytes taken by audio (copied as-is to the output).</summary>
    public long AudioBytes => (long)(Audio.Sum(a => a.Bitrate) / 8.0 * Duration);

    public string Describe() =>
        $"{VideoCodec} {Width}x{Height} {Fps:0.##}fps {PixFmt}{(IsHdr ? " HDR" : "")}, " +
        $"{TimeSpan.FromSeconds(Duration):hh\\:mm\\:ss}, {Format.Bytes(Size)}, " +
        $"{Audio.Count} audio / {Subtitles.Count} subtitle track(s)";

    public static async Task<MediaInfo> ProbeAsync(string file, CancellationToken ct)
    {
        var r = await Tools.RunAsync(Tools.Ffprobe,
            new[] { "-v", "error", "-print_format", "json", "-show_format", "-show_streams", file }, ct, collectStdout: true);
        if (r.ExitCode != 0) throw new Exception("Can't read file: " + Tools.Tail(r.Stderr, 3));

        var m = new MediaInfo { Path = file, Size = new FileInfo(file).Length };
        using var doc = JsonDocument.Parse(r.Stdout);
        var root = doc.RootElement;
        if (root.TryGetProperty("format", out var fmt)) m.Duration = Num(Str(fmt, "duration"));

        foreach (var s in root.GetProperty("streams").EnumerateArray())
        {
            int index = s.GetProperty("index").GetInt32();
            string codec = Str(s, "codec_name") ?? "unknown";
            long bitrate = (long)Num(Str(s, "bit_rate"));
            if (bitrate == 0 && s.TryGetProperty("tags", out var tags)) bitrate = (long)Num(Str(tags, "BPS") ?? Str(tags, "BPS-eng"));

            switch (Str(s, "codec_type"))
            {
                case "video":
                    bool attachedPic = s.TryGetProperty("disposition", out var d) &&
                                       d.TryGetProperty("attached_pic", out var ap) && ap.GetInt32() == 1;
                    if (attachedPic || m.VideoIndex >= 0) break;
                    m.VideoIndex = index;
                    m.VideoCodec = codec;
                    m.Width = s.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
                    m.Height = s.TryGetProperty("height", out var h) ? h.GetInt32() : 0;
                    m.PixFmt = Str(s, "pix_fmt") ?? "";
                    m.Fps = Rate(Str(s, "avg_frame_rate")) is > 0 and var avg ? avg : Rate(Str(s, "r_frame_rate"));
                    if (m.Fps <= 0 || m.Fps > 1000) m.Fps = 30;
                    m.Primaries = Known(Str(s, "color_primaries"));
                    m.Transfer = Known(Str(s, "color_transfer"));
                    m.Matrix = Known(Str(s, "color_space"));
                    m.Range = Known(Str(s, "color_range"));
                    m.FieldOrder = Str(s, "field_order");
                    m.VideoBitrate = bitrate;
                    m.VideoDuration = Num(Str(s, "duration"));
                    // MKV keeps per-track lengths in a tag ("01:23:45.678000000").
                    if (m.VideoDuration <= 0 && s.TryGetProperty("tags", out var vt)) m.VideoDuration = Clock(Str(vt, "DURATION") ?? Str(vt, "DURATION-eng"));
                    if (m.Duration <= 0) m.Duration = m.VideoDuration;
                    break;
                case "audio":
                    m.Audio.Add((index, codec, bitrate));
                    break;
                case "subtitle":
                    m.Subtitles.Add((index, codec));
                    break;
            }
        }
        if (m.VideoIndex < 0) throw new Exception("No video stream found.");
        return m;
    }

    static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString()) : null;

    static double Num(string? s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && !double.IsNaN(v) ? v : 0;

    static double Rate(string? s)
    {
        if (s == null) return 0;
        var parts = s.Split('/');
        return parts.Length == 2 && Num(parts[1]) > 0 ? Num(parts[0]) / Num(parts[1]) : Num(s);
    }

    static double Clock(string? s)
    {
        if (s == null) return 0;
        int dot = s.IndexOf('.');
        if (dot >= 0 && s.Length - dot > 8) s = s[..(dot + 8)]; // TimeSpan takes at most 7 fractional digits
        return TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out var t) ? t.TotalSeconds : 0;
    }

    /// <summary>Which HDR10 side data a file carries (mastering display, content light level), from the stream or first frame.</summary>
    public static async Task<HashSet<string>> HdrSideDataAsync(string file, CancellationToken ct)
    {
        var r = await Tools.RunAsync(Tools.Ffprobe, new[]
        {
            "-v", "error", "-select_streams", "v:0", "-read_intervals", "%+#1",
            "-show_entries", "stream_side_data=side_data_type:frame_side_data=side_data_type", "-of", "csv=p=0", file,
        }, ct, collectStdout: true);
        return r.Stdout.Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t is "Mastering display metadata" or "Content light level metadata").ToHashSet();
    }

    static string? Known(string? s) => string.IsNullOrEmpty(s) || s is "unknown" or "reserved" ? null : s;
}

static class Format
{
    public static string Bytes(long b)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = b;
        int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.##} {u[i]}";
    }
}
