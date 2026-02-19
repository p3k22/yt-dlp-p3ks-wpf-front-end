namespace yt_dlp_p3k_wpf.Models
{
   internal readonly record struct DownloadParameters(
      string Url,
      int Format,
      string Quality,
      string FileType,
      string OutputPath)
   {
   }
}