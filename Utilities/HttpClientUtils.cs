namespace yt_dlp_p3k_wpf.Utilities
{
   using System.Diagnostics;
   using System.IO;
   using System.Net.Http;
   using System.Windows;

   internal static class HttpClientUtils
   {
      private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(20);

      private static readonly HttpClient HttpClient = new() {Timeout = DownloadTimeout};

      /// <summary>
      /// Downloads a file from a URL to a local destination.
      /// </summary>
      /// <param name="url">The URL of the file to download.</param>
      /// <param name="destination">The local file path to save the downloaded file.</param>
      /// <returns>A task representing the asynchronous download operation.</returns>
      public static async Task DownloadFileAsync(string url, string destination)
      {
         if (string.IsNullOrEmpty(url))
         {
            MessageBox.Show("Download URL is null or empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
         }

         try
         {
            var directory = Path.GetDirectoryName(destination) ?? Directory.GetCurrentDirectory();

            if (!Directory.Exists(directory))
            {
               Directory.CreateDirectory(directory);
            }

            using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var fileStream = new FileStream(destination, FileMode.Create);
            await response.Content.CopyToAsync(fileStream);

            Debug.WriteLine($"Successfully downloaded {url} to \n{destination}");
         }
         catch (Exception ex)
         {
            MessageBox.Show(
            $"Failed to download file: {ex.Message}",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
         }
      }
   }
}
