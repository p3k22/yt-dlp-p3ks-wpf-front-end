namespace yt_dlp_p3k_wpf.Models
{
   using System.Diagnostics;
   using System.IO;

   /// <summary>
   ///    Manages yt-dlp download processes and streams their output.
   /// </summary>
   internal class Download
   {
      /// <summary>
      ///    Starts a yt-dlp download, captures stdout/stderr line-by-line, and
      ///    forwards each line to <paramref name="onOutputLine" />.
      /// </summary>
      /// <param name="binariesFilePath">Directory containing the yt-dlp and ffmpeg binaries.</param>
      /// <param name="ytdlpFileName">File name of the yt-dlp executable.</param>
      /// <param name="ffmpegFileName">File name of the ffmpeg executable.</param>
      /// <param name="downloadParams">Parameters describing the requested download.</param>
      /// <param name="onOutputLine">Callback invoked for every line of process output.</param>
      /// <returns>A task that completes when the download process exits.</returns>
      public static async Task StartDownloadAsync(
         string binariesFilePath,
         string ytdlpFileName,
         string ffmpegFileName,
         DownloadParameters downloadParams,
         Action<string>? onOutputLine = null)
      {
         var ytdlpPath = Path.Combine(binariesFilePath, ytdlpFileName);
         var ffmpegPath = Path.Combine(binariesFilePath, ffmpegFileName);

         var videoFormat = downloadParams.Quality == "best" ?
                              "bestvideo" :
                              $"bestvideo[height<={downloadParams.Quality}]";

         var arguments = downloadParams.Format switch
            {
               0 =>
                  $"-o \"{downloadParams.OutputPath}\" --no-playlist --no-warnings --newline --progress-template \"download:%(progress._percent_str)s %(progress._speed_str)s ETA %(progress._eta_str)s\" --ffmpeg-location \"{ffmpegPath}\" -f \"{videoFormat}+bestaudio\" --merge-output-format {downloadParams.FileType} --remux-video {downloadParams.FileType} \"{downloadParams.Url}\"",
               1 =>
                  $"-o \"{downloadParams.OutputPath}\" --no-playlist --no-warnings --newline --progress-template \"download:%(progress._percent_str)s %(progress._speed_str)s ETA %(progress._eta_str)s\" --ffmpeg-location \"{ffmpegPath}\" -f \"{videoFormat}\" --remux-video {downloadParams.FileType} \"{downloadParams.Url}\"",
               2 =>
                  $"-o \"{downloadParams.OutputPath}\" --no-playlist --no-warnings --newline --progress-template \"download:%(progress._percent_str)s %(progress._speed_str)s ETA %(progress._eta_str)s\" --ffmpeg-location \"{ffmpegPath}\" -x --audio-format {downloadParams.FileType} -f bestaudio \"{downloadParams.Url}\"",
               _ => throw new ArgumentOutOfRangeException(
                    nameof(downloadParams),
                    $"Unknown format: {downloadParams.Format}")
            };

         var psi = new ProcessStartInfo
            {
               FileName = ytdlpPath,
               Arguments = arguments,
               WorkingDirectory = binariesFilePath,
               CreateNoWindow = true,
               UseShellExecute = false,
               RedirectStandardOutput = true,
               RedirectStandardError = true
            };

         var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

         process.OutputDataReceived += (_, e) =>
            {
               if (!string.IsNullOrEmpty(e.Data))
               {
                  onOutputLine?.Invoke(e.Data);
               }
            };

         process.ErrorDataReceived += (_, e) =>
            {
               if (!string.IsNullOrEmpty(e.Data))
               {
                  onOutputLine?.Invoke(e.Data);
               }
            };

         process.Start();
         process.BeginOutputReadLine();
         process.BeginErrorReadLine();

         await process.WaitForExitAsync();
      }
   }
}
