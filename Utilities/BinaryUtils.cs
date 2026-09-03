namespace yt_dlp_p3k_wpf.Utilities
{
   using System.Diagnostics;
   using System.IO;
   using System.IO.Compression;
   using System.Windows;

   internal static class BinaryUtils
   {
      internal const string BINARIES_FOLDER = "Resources/Bin";

      internal const string FFMPEG_FILENAME = "ffmpeg.exe";

      internal const string FFMPEG_MASTER = "ffmpeg-master-latest-win64-gpl.zip";

      internal const string FFMPEG_URL =
         "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";

      internal const string FFPROBE_FILENAME = "ffprobe.exe";

      internal const string YTDLP_FILENAME = "yt-dlp.exe";

      internal const string YTDLP_URL = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";

      /// <summary>
      /// How long a yt-dlp binary is trusted before an update is attempted.
      /// YouTube changes break older builds roughly monthly: extraction keeps working,
      /// but the actual transfer dies partway through with "HTTP Error 403: Forbidden".
      /// </summary>
      private static readonly TimeSpan YtdlpMaxAge = TimeSpan.FromDays(7);

      /// <summary>
      /// Ensures required binaries are downloaded and available locally.
      /// </summary>
      /// <returns><c>true</c> when all required binaries exist; otherwise, <c>false</c>.</returns>
      internal static async Task<bool> TryLoadBinaries()
      {
         var bin = Path.Combine(Directory.GetCurrentDirectory(), BINARIES_FOLDER);
         Debug.WriteLine($"Searching for binaries at {bin}");

         await LoadYTDLPAsync(bin);
         await LoadFFMPEGAsync(bin);

         return BinaryExists(YTDLP_FILENAME) && BinaryExists(FFMPEG_FILENAME) && BinaryExists(FFPROBE_FILENAME);
      }

      /// <summary>
      /// Checks whether a binary exists in the configured binaries folder.
      /// </summary>
      /// <param name="fileName">The file name to check.</param>
      /// <returns><c>true</c> when the file exists; otherwise, <c>false</c>.</returns>
      private static bool BinaryExists(string fileName)
      {
         var filePath = Path.Combine(Directory.GetCurrentDirectory(), BINARIES_FOLDER, fileName);
         if (!File.Exists(filePath))
         {
            Debug.WriteLine($"File not found: {filePath}");
            return false;
         }

         return true;
      }

      /// <summary>
      /// Downloads and extracts FFmpeg binaries when they are missing.
      /// </summary>
      /// <param name="bin">The binaries folder path.</param>
      /// <returns>A task that completes when the binaries are available.</returns>
      private static async Task LoadFFMPEGAsync(string bin)
      {
         var ffmpegZip = Path.Combine(bin, FFMPEG_MASTER);
         if (BinaryExists(FFMPEG_FILENAME) && BinaryExists(FFPROBE_FILENAME))
         {
            return;
         }

         Debug.WriteLine($"Downloading {FFMPEG_MASTER} from {FFMPEG_URL} to \n{ffmpegZip}");
         await HttpClientUtils.DownloadFileAsync(FFMPEG_URL, ffmpegZip);
         await UnzipFFMPEG(ffmpegZip, bin);
      }

      /// <summary>
      /// Downloads the yt-dlp binary when it is missing, and refreshes it when the local
      /// copy has gone stale.
      /// </summary>
      /// <param name="bin">The binaries folder path.</param>
      /// <returns>A task that completes when the binary is available and reasonably current.</returns>
      private static async Task LoadYTDLPAsync(string bin)
      {
         var downloadDestination = Path.Combine(bin, YTDLP_FILENAME);

         if (!BinaryExists(YTDLP_FILENAME))
         {
            Debug.WriteLine($"Downloading {YTDLP_FILENAME} from {YTDLP_URL} to \n{downloadDestination}");
            await HttpClientUtils.DownloadFileAsync(YTDLP_URL, downloadDestination);
            return;
         }

         await UpdateYTDLPIfStaleAsync(downloadDestination);
      }

      /// <summary>
      /// Runs yt-dlp's own self-updater when the local binary is older than
      /// <see cref="YtdlpMaxAge" />, falling back to a fresh download if that fails.
      /// </summary>
      /// <param name="ytdlpPath">Full path to the yt-dlp executable.</param>
      /// <returns>A task that completes once the update attempt has finished.</returns>
      private static async Task UpdateYTDLPIfStaleAsync(string ytdlpPath)
      {
         try
         {
            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(ytdlpPath);
            if (age < YtdlpMaxAge)
            {
               Debug.WriteLine($"{YTDLP_FILENAME} is {age.TotalDays:F1} days old; skipping update check.");
               return;
            }

            Debug.WriteLine($"{YTDLP_FILENAME} is {age.TotalDays:F1} days old; running self-update.");

            var psi = new ProcessStartInfo
               {
                  FileName = ytdlpPath,
                  Arguments = "-U",
                  WorkingDirectory = Path.GetDirectoryName(ytdlpPath) ?? Directory.GetCurrentDirectory(),
                  CreateNoWindow = true,
                  UseShellExecute = false,
                  RedirectStandardOutput = true,
                  RedirectStandardError = true
               };

            using var process = Process.Start(psi);
            if (process == null)
            {
               await RedownloadYTDLPAsync(ytdlpPath, "could not start the updater");
               return;
            }

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            Debug.WriteLine($"yt-dlp -U exited {process.ExitCode}: {stdout}{stderr}");

            if (process.ExitCode != 0)
            {
               await RedownloadYTDLPAsync(ytdlpPath, $"updater exited {process.ExitCode}");
               return;
            }

            // "-U" leaves the file untouched when it is already current, which would make
            // every launch re-check. Stamp it so the age test means "last verified".
            File.SetLastWriteTimeUtc(ytdlpPath, DateTime.UtcNow);
         }
         catch (Exception exception)
         {
            // A failed update must never stop the app starting -- the existing binary may
            // still work perfectly well.
            Debug.WriteLine($"yt-dlp update check failed: {exception.Message}");
         }
      }

      /// <summary>
      /// Replaces the yt-dlp binary with a fresh copy from the official release URL.
      /// </summary>
      /// <param name="ytdlpPath">Full path to the yt-dlp executable.</param>
      /// <param name="reason">Why the self-updater was not used.</param>
      /// <returns>A task that completes when the download has finished.</returns>
      private static async Task RedownloadYTDLPAsync(string ytdlpPath, string reason)
      {
         Debug.WriteLine($"Self-update unavailable ({reason}); re-downloading {YTDLP_FILENAME}.");
         await HttpClientUtils.DownloadFileAsync(YTDLP_URL, ytdlpPath);

         if (File.Exists(ytdlpPath))
         {
            File.SetLastWriteTimeUtc(ytdlpPath, DateTime.UtcNow);
         }
      }

      /// <summary>
      /// Extracts FFmpeg binaries from a zip archive.
      /// </summary>
      /// <param name="zipFilePath">The path to the zip archive.</param>
      /// <param name="destination">The destination directory for extraction.</param>
      /// <returns>A task that completes when extraction and cleanup finishes.</returns>
      private static async Task UnzipFFMPEG(string zipFilePath, string destination)
      {
         var file = ZipFile.ExtractToDirectoryAsync(zipFilePath, destination, true);
         await file;
         if (file.IsCompletedSuccessfully)
         {
            var ffmpegUnzippedDir = Path.Combine(destination, zipFilePath.Replace(".zip", ""));

            Debug.WriteLine($"Successfully extracted {zipFilePath} to \n{ffmpegUnzippedDir}");

            var moveFrom = Path.Combine(destination, @$"{ffmpegUnzippedDir}\bin");
            var file1 = Path.Combine(moveFrom, FFMPEG_FILENAME);
            var file2 = Path.Combine(moveFrom, FFPROBE_FILENAME);

            if (File.Exists(file1) && File.Exists(file2))
            {
               File.Move(file1, Path.Combine(destination, FFMPEG_FILENAME), true);
               File.Move(file2, Path.Combine(destination, FFPROBE_FILENAME), true);

               File.Delete(zipFilePath);
               Directory.Delete(ffmpegUnzippedDir, true);
            }
         }
         else if (file.IsFaulted)
         {
            MessageBox.Show(
            $"Failed to extract file: {file.Exception?.Message}",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
         }
      }
   }
}