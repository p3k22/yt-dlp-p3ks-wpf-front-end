# yt-dlp.p3k.local

A lightweight Windows desktop front-end for downloading video and audio from YouTube and [thousands of other sites](https://github.com/yt-dlp/yt-dlp/blob/master/supportedsites.md). Built with WPF on .NET 10 and powered by [yt-dlp](https://github.com/yt-dlp/yt-dlp) and [ffmpeg](https://github.com/yt-dlp/FFmpeg-Builds).

---

## Features

- **Video + Audio, Video Only, or Audio Only** — pick the media streams you need
- **Quality selection** — Best, 1080p, 720p, or 480p
- **Multiple output formats** — mp4 / mkv / webm for video; mp3 / m4a / ogg for audio
- **Real-time progress** — live percentage and status parsed from yt-dlp output and reflected in the UI
- **Auto-download binaries** — yt-dlp and ffmpeg are downloaded automatically on first launch; no manual setup required
- **Output to Downloads folder** — finished files are saved directly to the user's `Downloads` directory
- **Dark theme** — custom dark UI built with WPF, JetBrains Mono, and Outfit fonts; no third-party UI library required

---

## Prerequisites

| Requirement | Notes |
|-------------|-------|
| Windows | WPF application — Windows only |
| [.NET 10 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) | Required to run the application |

> **Binaries are downloaded automatically.**
> On first launch the app downloads `yt-dlp.exe` and `ffmpeg-master-latest-win64-gpl.zip` (extracting `ffmpeg.exe` and `ffprobe.exe`) from their official GitHub release pages. All binaries are stored in `Resources/Bin/` inside the application directory.

---

## Quick Start

### 1. Clone and build

```bash
git clone https://github.com/p3k22/yt-dlp-p3ks-wpf-front-end
cd yt-dlp-p3ks-wpf-front-end
dotnet run
```

Or open `yt-dlp-p3ks-wpf-front-end.csproj` in Visual Studio 2022+ and press **F5**.

### 2. First launch

On first launch the application automatically downloads the required binaries:

- `yt-dlp.exe` from [yt-dlp releases](https://github.com/yt-dlp/yt-dlp/releases/latest)
- `ffmpeg.exe` + `ffprobe.exe` from [yt-dlp/FFmpeg-Builds](https://github.com/yt-dlp/FFmpeg-Builds/releases/latest)

They are stored in `Resources/Bin/` next to the executable. The **GET** button becomes active once all binaries are confirmed present.

### 3. Download media

1. Paste a URL into the input field.
2. Select **Format** (Video + Audio / Video Only / Audio Only).
3. Select **Quality** (Best / 1080p / 720p / 480p) — hidden for Audio Only.
4. Select **File Type** (mp4 / mkv / webm for video; mp3 / m4a / ogg for audio).
5. Click **GET**.

The output file is saved to the user's `Downloads` folder.

---

## Project Structure

```
yt-dlp-p3ks-wpf-front-end.csproj
├── App.xaml / App.xaml.cs          Application entry point and font resources
├── Views/
│   ├── MainWindow.xaml             Main UI — input bar, format/quality/file-type toggles, log panel
│   └── MainWindow.xaml.cs         Code-behind — INotifyPropertyChanged, toggle logic, download orchestration
├── Models/
│   ├── Download.cs                 Spawns and streams yt-dlp as a child process
│   └── DownloadParameters.cs      Record struct holding URL, format, quality, file type, and output path
├── Utilities/
│   ├── BinaryUtils.cs             Checks for, downloads, and extracts yt-dlp and ffmpeg binaries
│   └── HttpClientUtils.cs         Shared HttpClient wrapper for file downloads
└── Resources/
    ├── Bin/                        Auto-populated at runtime (yt-dlp.exe, ffmpeg.exe, ffprobe.exe)
    └── Fonts/                      Bundled JetBrains Mono and Outfit font files
```

---

## How It Works

1. On startup `BinaryUtils.TryLoadBinaries()` checks `Resources/Bin/` for `yt-dlp.exe`, `ffmpeg.exe`, and `ffprobe.exe`. Any missing binary is downloaded from its official GitHub release URL.
2. The user pastes a URL and selects format, quality, and file-type options.
3. On clicking **GET**, `Download.StartDownloadAsync()` builds the yt-dlp argument string and spawns a child process via `ProcessStartInfo` with stdout/stderr redirected.
4. Each output line is dispatched back to the UI thread and passed to `HandleOutputLine()`, which parses progress percentages, destination filenames, and phase labels (downloading, merging) to update the progress bar and status text.
5. When the process exits the status is set to `done — <filename>` (success) or an error message (failure).

---

## Configuration

There is no configuration file. All behaviour is controlled through the UI toggles and the following fixed defaults:

| Setting | Value | Notes |
|---------|-------|-------|
| Binary directory | `Resources/Bin/` | Relative to the executable; created automatically |
| Output directory | `%USERPROFILE%\Downloads` | Fixed; uses `%(title)s.%(ext)s` naming |
| Binary download timeout | 60 seconds | Configurable in `LoadBinariesAsync()` |
| HTTP client timeout | 20 seconds | Defined in `HttpClientUtils` |

---

## License

MIT