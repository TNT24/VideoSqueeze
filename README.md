# VideoSqueeze

Shrinks videos **without visible quality loss** — and proves it.

Instead of guessing a CRF/bitrate like most encoders (Shutter Encoder, HandBrake…), VideoSqueeze:

1. Cuts a few short samples spread through the video.
2. Encodes them at different quality levels.
3. Scores each one against the original with **VMAF** (Netflix's perceptual quality metric, 0–100).
4. Picks the *smallest* setting that still scores ≥ your target (default **95** ≈ indistinguishable).
5. Estimates the final size and **skips** files that wouldn't get meaningfully smaller.

Audio, subtitles, chapters, metadata and attachments are copied untouched. Originals are never modified.

## Use

Install with `installer\VideoSqueeze-Setup-<version>.exe` (adds a Start menu entry and an uninstaller under Settings › Apps), then drag videos/folders onto the list, pick a **preset**, then:

- **Estimate savings** — checks a few samples per file (minutes, no full encode) and shows the expected
  saving per file plus a total. **Start** reuses these results, so nothing is wasted.
- **Start** — compresses. Output goes next to each original as `name_squeezed.mkv` (or to the folder you choose).

| Preset | Use for |
|---|---|
| Fast — NVIDIA GPU (recommended) | Most files. ≈25 min per 2-hour 1080p movie. |
| Smallest files — AV1 | Overnight runs; smallest output. ≈3–4 h per 2-hour movie. |
| Plays everywhere — HEVC | Older TVs/phones. ≈3 h per 2-hour movie. |

All presets target VMAF 95 and output MKV. Choose **Custom…** for full control:

| Setting | Recommendation |
|---|---|
| Mode | *Visually lossless — automatic*. "Mathematically lossless" is bit-exact but usually makes camera/phone files **bigger**. |
| Codec | **AV1** = smallest files (slow). **HEVC x265** = plays on nearly every TV/phone. **NVIDIA GPU** = 5–20× faster, ~15–30% larger. |
| Target VMAF | 95 default. 93 = smaller, very close. 97 = extra safety margin. |
| Container | MKV keeps every track. MP4 for maximum device compatibility. |

## Requirements

- 64-bit Windows (the .NET runtime is bundled).
- FFmpeg with libvmaf — found automatically from Shutter Encoder's install, next to the exe, or on PATH.

## Updates

VideoSqueeze checks this repository's [releases](https://github.com/TNT24/VideoSqueeze/releases) when it starts and
every 6 hours while open. A newer version shows a bar at the top of the window; **Update** downloads it (checked
against GitHub's SHA-256 digest), installs it and reopens the app. A running encode is never interrupted.

## Build

```
.\build-installer.ps1           # build the installer
.\build-installer.ps1 -Publish  # …and publish it as a GitHub release (needs the gh CLI, signed in)
```

Publishes a self-contained exe to `build\` and packages it with [Inno Setup](https://jrsoftware.org/isinfo.php)
(`winget install JRSoftware.InnoSetup`) into `installer\`. Bump `<Version>` in `VideoSqueeze.csproj` for a new release.

Tests: `dotnet test tests/VideoSqueeze.Tests` (encodes small generated clips; needs FFmpeg).

Set `VIDEOSQUEEZE_DEBUG=1` to print every ffmpeg command to the console.

## License

MIT — see [LICENSE](LICENSE).
