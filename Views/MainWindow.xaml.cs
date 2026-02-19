namespace yt_dlp_p3k_wpf
{
   using System.Collections.ObjectModel;
   using System.ComponentModel;
   using System.Diagnostics;
   using System.Globalization;
   using System.IO;
   using System.Runtime.CompilerServices;
   using System.Windows;
   using System.Windows.Controls;
   using System.Windows.Navigation;

   using yt_dlp_p3k_wpf.Models;
   using yt_dlp_p3k_wpf.Utilities;

   /// <summary>
   ///    Interaction logic for MainWindow.xaml
   /// </summary>
   public partial class MainWindow : INotifyPropertyChanged
   {
      private string _downloadedFileName = string.Empty;

      private bool _hasTimedOut;

      private bool _isDisplayingUrlWarning;

      public bool FoundRequiredBinaries
      {
         get;
         private set
         {
            field = value;
            OnPropertyChanged();
         }
      }

      /// <summary>
      ///    Gets a value indicating whether the log panel is visible.
      /// </summary>
      /// <value><c>true</c> after a download has started; otherwise, <c>false</c>.</value>
      public bool LogPanelVis
      {
         get;
         private set
         {
            field = value;
            OnPropertyChanged();
         }
      } = false;

      public bool UrlWarningVis
      {
         get;
         private set
         {
            field = value;
            OnPropertyChanged();
         }
      } = false;

      /// <summary>
      ///    Gets the current download progress percentage (0–100).
      /// </summary>
      public double DownloadProgress
      {
         get;
         private set
         {
            field = value;
            OnPropertyChanged();
            ProgressScale.ScaleX = value / 100.0;
         }
      }

      /// <summary>
      ///    Gets the height for the Quality options row.
      /// </summary>
      /// <value>A <see cref="GridLength" /> of 70 when quality controls are visible, or 0 when hidden.</value>
      public GridLength QualityRowHeight
      {
         get;
         private set
         {
            field = value;
            OnPropertyChanged();
         }
      } = new(70);

      /// <summary>
      ///    Gets the collection of log lines displayed in the output panel.
      /// </summary>
      public ObservableCollection<string> LogMessages { get; } = [];

      /// <summary>
      ///    Gets the status style that controls the status text colour.
      /// </summary>
      /// <value>
      ///    <c>"normal"</c> (dim grey) during download,
      ///    <c>"success"</c> (accent green) on completion,
      ///    <c>"error"</c> (red) on failure.
      /// </value>
      public string StatusStyle
      {
         get;
         private set
         {
            field = value;
            OnPropertyChanged();
         }
      } = "normal";

      /// <summary>
      ///    Gets the status text displayed alongside the progress bar (speed + ETA).
      /// </summary>
      public string StatusText
      {
         get;
         private set
         {
            field = value;
            OnPropertyChanged();
         }
      } = string.Empty;

      public string UrlWarningText
      {
         get;
         private set
         {
            field = value;
            OnPropertyChanged();
         }
      } = string.Empty;

      /// <summary>
      ///    Raised when a bound property value changes.
      /// </summary>
      public event PropertyChangedEventHandler? PropertyChanged;

      /// <summary>
      ///    Initializes a new instance of <see cref="MainWindow" /> and sets the default active button in each option group.
      /// </summary>
      public MainWindow()
      {
         DataContext = this;
         InitializeComponent();
         SetActiveButtonInGroup(Vid_Aud, Vid_Aud, Vid, Aud);
         SetActiveButtonInGroup(QBest, QBest, Q1080p, Q720p, Q480);
         SetActiveButtonInGroup(mp4, mp4, mkv, webm);
         SetActiveButtonInGroup(mp3, mp3, m4a, ogg);

         _ = LoadBinariesAsync();
      }

      /// <summary>
      ///    Appends a message to the log panel and scrolls to the bottom.
      /// </summary>
      /// <param name="message">The log line to append.</param>
      private void AppendLog(string message)
      {
         LogMessages.Add(message);
         LogScrollViewer.ScrollToEnd();
      }

      /// <summary>
      ///    Shows or hides the video file-type panel, audio file-type panel, and quality row
      ///    based on the <see cref="System.Windows.FrameworkElement.Name" /> of <paramref name="clicked" />.
      /// </summary>
      /// <param name="clicked">The format <see cref="Button" /> whose <c>Name</c> determines which panels are shown.</param>
      private void ChangeFormat(Button clicked)
      {
         switch (clicked.Name)
         {
            case "Aud":
               VideoFileTypes.Visibility = Visibility.Collapsed;
               Quality.Visibility = Visibility.Hidden;
               QualityRowHeight = new GridLength(0);
               AudioFileTypes.Visibility = Visibility.Visible;
               break;
            case "Vid":
            case "Vid_Aud":
               VideoFileTypes.Visibility = Visibility.Visible;
               Quality.Visibility = Visibility.Visible;
               QualityRowHeight = new GridLength(70);
               AudioFileTypes.Visibility = Visibility.Collapsed;
               break;
         }
      }

      private async Task DisableUrlWarningAsync()
      {
         await Task.Delay(5000);
         UrlWarningVis = false;
         _isDisplayingUrlWarning = false;
      }

      /// <summary>
      ///    Processes a single line of yt-dlp output, detecting phases and
      ///    progress updates to mirror the PHP front-end behaviour.
      /// </summary>
      /// <param name="line">A raw stdout/stderr line from the yt-dlp process.</param>
      private void HandleOutputLine(string line)
      {
         var trimmed = line.TrimStart();

         // Progress line from --progress-template: "  45.2%  5.23MiB/s ETA 00:12"
         // The "download:" in the template arg is a type selector — not part of output.
         if (trimmed.Length > 0 && char.IsDigit(trimmed[0]))
         {
            var pctEnd = trimmed.IndexOf('%');
            if (pctEnd > 0 && double.TryParse(
                trimmed[..pctEnd],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var pct))
            {
               DownloadProgress = pct;
               StatusText = $"{pct:F1}%";
            }
         }
         // Capture output filename from destination line
         else if (trimmed.StartsWith("[download] Destination:", StringComparison.Ordinal))
         {
            _downloadedFileName = Path.GetFileName(trimmed["[download] Destination:".Length..].Trim());
         }
         // Phase: new stream download starting
         else if (trimmed.StartsWith("[download] Downloading", StringComparison.OrdinalIgnoreCase))
         {
            var label = trimmed.Contains("video", StringComparison.OrdinalIgnoreCase) ? "video" : "audio";
            StatusText = $"downloading ({label})...";
            DownloadProgress = 0;
         }
         // Phase: merging / muxing
         else if (trimmed.StartsWith("[Merger]", StringComparison.Ordinal)
                  || trimmed.StartsWith("[Mux]", StringComparison.Ordinal)
                  || trimmed.StartsWith("[FixupM", StringComparison.Ordinal))
         {
            StatusText = "merging streams...";
            DownloadProgress = 100;
         }

         AppendLog(line);
      }

      private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
      {
         Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) {UseShellExecute = true});
         e.Handled = true;
      }

      /// <summary>
      ///    Downloads the required binaries for the application.
      /// </summary>
      /// <param name="timeout">Default 60 seconds</param>
      /// <returns></returns>
      private async Task LoadBinariesAsync(int timeout = 60000)
      {
         var cts = new CancellationTokenSource(timeout);
         try
         {
            while (!FoundRequiredBinaries)
            {
               FoundRequiredBinaries = await BinaryUtils.TryLoadBinaries();
               if (FoundRequiredBinaries)
               {
                  Debug.WriteLine("All required binaries found and loaded successfully.");
                  break;
               }

               await Task.Delay(1000, cts.Token);
            }
         }
         catch (Exception e)
         {
            _hasTimedOut = cts.IsCancellationRequested;
            MessageBox.Show(
            e.Message,
            "Error - Required binaries not found",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
         }
      }

      /// <summary>
      ///    Handles clicks for the Audio file-type row (mp3 / m4a / ogg).
      /// </summary>
      /// <param name="sender">The <see cref="Button" /> that was clicked.</param>
      /// <param name="e">Event data for the routed event.</param>
      private void OnClick_AudioFileTypeButton(object sender, RoutedEventArgs e)
      {
         SetActiveButtonInGroup((Button) sender, mp3, m4a, ogg);
      }

      /// <summary>
      ///    Handles the Download button click to start the download process.
      /// </summary>
      /// <param name="sender">The <see cref="Button" /> that was clicked.</param>
      /// <param name="e">Event data for the routed event.</param>
      private void OnClick_DownloadButton(object sender, RoutedEventArgs e)
      {
         if (string.IsNullOrEmpty(Input.Text))
         {
            ShowUrlWarning("* Enter a valid URL");
            return;
         }

         if (!FoundRequiredBinaries && !_hasTimedOut)
         {
            ShowUrlWarning("* Cannot start download: Required binaries are being downloaded.");
            return;
         }

         if (!FoundRequiredBinaries && _hasTimedOut)
         {
            ShowUrlWarning("* Cannot start download: Required binaries are missing. Restart the application.");
            return;
         }

         var bin = Path.Combine(Directory.GetCurrentDirectory(), BinaryUtils.BINARIES_FOLDER);
         var ytdlpName = BinaryUtils.YTDLP_FILENAME;
         var ffmpegFileName = BinaryUtils.FFMPEG_FILENAME;

         var format = Vid_Aud.Tag != null ? 0 : Vid.Tag != null ? 1 : 2;
         var quality = QBest.Tag != null ? "best" : Q1080p.Tag != null ? "1080" : Q720p.Tag != null ? "720" : "480";
         var fileType = format == 2 ? mp3.Tag != null ? "mp3" : m4a.Tag != null ? "m4a" : "vorbis" :
                        mp4.Tag != null ? "mp4" :
                        mkv.Tag != null ? "mkv" : "webm";
         var outputPath = Path.Combine(
         Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
         "Downloads",
         "%(title)s.%(ext)s");

         var downloadParams = new DownloadParameters(Input.Text, format, quality, fileType, outputPath);

         LogMessages.Clear();
         LogPanelVis = true;
         DownloadProgress = 0;
         StatusText = "starting...";
         StatusStyle = "normal";
         _downloadedFileName = string.Empty;
         AppendLog("Starting download...");

         _ = Download.StartDownloadAsync(
         bin,
         ytdlpName,
         ffmpegFileName,
         downloadParams,
         line => { Dispatcher.Invoke(() => HandleOutputLine(line)); }).ContinueWith(t =>
            {
               Dispatcher.Invoke(() =>
                  {
                     if (t.IsFaulted)
                     {
                        StatusText = t.Exception?.InnerException?.Message ?? "download failed";
                        StatusStyle = "error";
                     }
                     else
                     {
                        DownloadProgress = 100;
                        StatusText = string.IsNullOrEmpty(_downloadedFileName)
                                        ? "done"
                                        : $"done \u2014 {_downloadedFileName}";
                        StatusStyle = "success";
                     }
                  });
            });
      }

      /// <summary>
      ///    Handles clicks for the Format row (Video+Audio / Video / Audio).
      /// </summary>
      /// <param name="sender">The <see cref="Button" /> that was clicked.</param>
      /// <param name="e">Event data for the routed event.</param>
      private void OnClick_FormatButton(object sender, RoutedEventArgs e)
      {
         SetActiveButtonInGroup((Button) sender, Vid_Aud, Vid, Aud);
      }

      /// <summary>
      ///    Handles clicks for the Quality row (Best / 1080p / 720p / 480p).
      /// </summary>
      /// <param name="sender">The <see cref="Button" /> that was clicked.</param>
      /// <param name="e">Event data for the routed event.</param>
      private void OnClick_QualityButton(object sender, RoutedEventArgs e)
      {
         SetActiveButtonInGroup((Button) sender, QBest, Q1080p, Q720p, Q480);
      }

      /// <summary>
      ///    Handles clicks for the Video file-type row (mp4 / mkv / webm).
      /// </summary>
      /// <param name="sender">The <see cref="Button" /> that was clicked.</param>
      /// <param name="e">Event data for the routed event.</param>
      private void OnClick_VideoFileTypeButton(object sender, RoutedEventArgs e)
      {
         SetActiveButtonInGroup((Button) sender, mp4, mkv, webm);
      }

      /// <summary>
      ///    Raises the <see cref="PropertyChanged" /> event for the specified property.
      /// </summary>
      /// <param name="name">The name of the property that changed; supplied automatically by the compiler.</param>
      private void OnPropertyChanged([CallerMemberName] string? name = null)
      {
         PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
      }

      /// <summary>
      ///    Marks the clicked button as active and clears the active state from all others in the group.
      /// </summary>
      /// <param name="clicked">The button that was clicked.</param>
      /// <param name="group">All buttons belonging to the same option group.</param>
      private void SetActiveButtonInGroup(Button clicked, params Button[] group)
      {
         foreach (var btn in group)
         {
            btn.Tag = null;
         }

         clicked.Tag = "active";
         ChangeFormat(clicked);
      }

      private void ShowUrlWarning(string message)
      {
         if (_isDisplayingUrlWarning)
         {
            return;
         }

         UrlWarningText = message;
         UrlWarningVis = true;
         _isDisplayingUrlWarning = true;
         _ = DisableUrlWarningAsync();
      }
   }
}