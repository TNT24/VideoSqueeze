using System.Text.Json;

namespace VideoSqueeze;

/// <summary>A finished stretch of encoded video (video track only, starts at 0) covering <see cref="Duration"/> seconds.</summary>
public record Segment(string File, double Duration);

/// <summary>
/// An encode that was cut short (app closed, PC restarted, crash) and can carry on later. The video so far is
/// <see cref="Segments"/> plus whatever <see cref="Current"/> got to; resuming keeps the part of Current up to its last
/// keyframe as one more segment and encodes the rest. The finished file is the segments joined, with audio, subtitles
/// and chapters taken straight from the original (so only the picture has joins, and they fall on keyframes).
/// </summary>
public record ResumePoint(string Source, string SourceId, string ArgsKey, int Quality, string OutPath, string Current, List<Segment> Segments)
{
    public double Done => Segments.Sum(x => x.Duration);
    public IEnumerable<string> Files => Segments.Select(x => x.File).Append(Current);
}

/// <summary>The unfinished encodes, in %LOCALAPPDATA%\VideoSqueeze\resume.json, keyed by <see cref="Squeezer.SourceId"/>.</summary>
public static class ResumeStore
{
    public static string FilePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoSqueeze", "resume.json");

    static readonly object gate = new();

    static Dictionary<string, ResumePoint> Load()
    {
        try { if (File.Exists(FilePath)) return JsonSerializer.Deserialize<Dictionary<string, ResumePoint>>(File.ReadAllText(FilePath)) ?? new(); }
        catch { }
        return new();
    }

    static void Store(Dictionary<string, ResumePoint> all)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(all));
        File.Move(tmp, FilePath, true); // never leave a half-written file behind if the PC goes off mid-save
    }

    public static ResumePoint? Get(string src)
    {
        if (!File.Exists(src)) return null;
        lock (gate) return Load().GetValueOrDefault(Squeezer.SourceId(src));
    }

    public static void Save(ResumePoint p)
    {
        lock (gate)
        {
            var all = Load();
            all[p.SourceId] = p;
            Store(all);
        }
    }

    /// <summary>Forgets the source's unfinished encode and deletes its files.</summary>
    public static void Discard(string src)
    {
        lock (gate)
        {
            var all = Load();
            var id = Squeezer.SourceId(src);
            if (!all.Remove(id, out var p)) return;
            foreach (var f in p.Files) try { File.Delete(f); } catch { }
            Store(all);
        }
    }

    /// <summary>An unfinished encode of another video is going to be saved as <paramref name="outPath"/>.</summary>
    public static bool Claims(string outPath)
    {
        lock (gate) return Load().Values.Any(p => string.Equals(p.OutPath, outPath, StringComparison.OrdinalIgnoreCase));
    }
}
