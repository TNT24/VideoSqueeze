# VideoSqueeze

Shrinks videos **without visible quality loss** â€” and proves it.

Instead of guessing a CRF/bitrate like most encoders (Shutter Encoder, HandBrakeâ€¦), VideoSqueeze:

1. Cuts a few short samples spread through the video.
2. Encodes them at different quality levels.
3. Scores each one against the original with **VMAF** (Netflix's perceptual quality metric, 0â€“100).
4. Picks the *smallest* setting that still scores â‰¥ your target (default **95** â‰ˆ indistinguishable).
5. Estimates the final size and **skips** files that wouldn't get meaningfully smaller.

Audio, subtitles, chapters, metadata and attachments are copied untouched. Originals are never modified.

## Use

Install with `installer\VideoSqueeze-Setup-<version>.exe` (adds a Start menu entry and an uninstaller under Settings › Apps), then drag videos/folders onto the list, pick a **preset**, then:

- **Estimate savings** â€” checks a few samples per file (minutes, no full encode) and shows the expected
  saving per file plus a total. **Start** reuses these results, so nothing is wasted.
- **Start** â€” compresses. Output goes next to each original as `name_squeezed.mkv` (or to the folder you choose).

| Preset | Use for |
|---|---|
| Fast â€” NVIDIA GPU (recommended) | Most files. â‰ˆ25 min per 2-hour 1080p movie. |
| Smallest files â€” AV1 | Overnight runs; smallest output. â‰ˆ3â€“4 h per 2-hour movie. |
| Plays everywhere â€” HEVC | Older TVs/phones. â‰ˆ3 h per 2-hour movie. |

All presets target VMAF 95 and output MKV. Choose **Customâ€¦** for full control:

| Setting | Recommendation |
|---|---|
| Mode | *Visually lossless â€” automatic*. "Mathematically lossless" is bit-exact but usually makes camera/phone files **bigger**. |
| Codec | **AV1** = smallest files (slow). **HEVC x265** = plays on nearly every TV/phone. **NVIDIA GPU** = 5â€“20Ã— faster, ~15â€“30% larger. |
| Target VMAF | 95 default. 93 = smaller, very close. 97 = extra safety margin. |
| Container | MKV keeps every track. MP4 for maximum device compatibility. |

## Requirements

- 64-bit Windows (the .NET runtime is bundled).
- FFmpeg with libvmaf â€” found automatically from Shutter Encoder's install, next to the exe, or on PATH.

## Build

```
.\build-installer.ps1
```

Publishes a self-contained exe to `build\` and packages it with [Inno Setup](https://jrsoftware.org/isinfo.php)
(`winget install JRSoftware.InnoSetup`) into `installer\`. Bump `<Version>` in `VideoSqueeze.csproj` for a new release.

Set `VIDEOSQUEEZE_DEBUG=1` to print every ffmpeg command to the console.
