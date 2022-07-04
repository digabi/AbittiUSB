using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.Win32;
using Ytl.VerificationUtils;

namespace Ytl.AbittiUsb
{
    public partial class DownloadNewVersionUC
    {
        private readonly Panel canvas;
        private readonly DownloadConfigs configs;
        private Activity activity;

        public DownloadNewVersionUC(DownloadConfigs configs, Panel canvas, Activity activity)
        {
            InitializeComponent();
            this.configs = configs;
            this.canvas = canvas;
            this.activity = activity;

            LocalizationManager.SetWatch(configs.GetResourceKey("HeaderForDownload"), HeaderTextBlock);
            LocalizationManager.SetWatch(configs.GetResourceKey("DownloadNewVersion"), StartDownLoadingButtonText);
            CheckDiskSpace();
        }

        private void CheckDiskSpace()
        {
            const long GigaByte = 1024*1024*1024;
            var freeSpace = GetFreespaceForLocalAppDataDrive();

            if (freeSpace < 8*GigaByte)
            {
                LocalizationManager.SetWatch("InsufficientDiskSpace",
                    InsufficientDiskSpaceInfoText,
                    () => {
                        var byteSuffix = LocalizationManager.GetLocalizedValue<string>("ByteSuffix");
                        return FileVersionUtils.FormatFileSizeToHumanReadable(freeSpace, byteSuffix);
                    });
                // XXX Following should not be necessary, but for some reason the text is not updated correctly first time.
                LocalizationManager.SetWatch("RecheckDiskSpace", RecheckDiskSpaceText);
                StartDownLoadingButton.Visibility = Visibility.Collapsed;
                InsufficientDiskSpacePanel.Visibility = Visibility.Visible;
            }
            else
            {
                StartDownLoadingButton.Visibility = Visibility.Visible;
                InsufficientDiskSpacePanel.Visibility = Visibility.Collapsed;
            }
        }

        private static long GetFreespaceForLocalAppDataDrive()
        {
            var localApplicationData = VerificationEnvironment.DirectoryPath;
            var pathRoot = Path.GetPathRoot(localApplicationData);

            var freeSpace = DriveInfo.GetDrives()
                .Where(drive => drive.IsReady && drive.Name.Equals(pathRoot, StringComparison.OrdinalIgnoreCase))
                .Select(drive => drive.TotalFreeSpace)
                .First();
            return freeSpace;
        }

        private void StartDownloadClick(object sender, RoutedEventArgs e)
        {
            StartDownload();
        }

        private async void StartDownload()
        {
            var act = activity.Start();
            File.Delete(configs.ImageMode.ImageFilePath);
            File.Delete(configs.ImageMode.ImageFileMd5CalculatedCachePath);
            InfoPanel.Visibility = Visibility.Visible;
            StartDownLoadingButton.Visibility = Visibility.Collapsed;
            SetStatusInfo("EvaluateTimeLeft");
            ProgressBarTextBlock.Text = "0%";

            var contentLengthStream = HttpVersionUtils.GetContentLength(configs.ImageMode).Publish();
            contentLengthStream.Connect();
            var totalDownloadedStream = WireTotalDownloadedStream();
            var progressStream = WireProgressStream(contentLengthStream, totalDownloadedStream);
            var timeLeftStream = WireTimeLeftStream(contentLengthStream, totalDownloadedStream);
            var unzippingStream = WireUnzippingStream(totalDownloadedStream, await GetHash());
            WireUiSubscriptions(totalDownloadedStream, progressStream, timeLeftStream, unzippingStream);
            unzippingStream.Subscribe(x => { }, x => act.Dispose(), () => act.Dispose());
        }

        private Task<string> GetHash()
        {
            return HttpVersionUtils.GetVersionHashAsync(configs.ImageMode.ImageFile);
        }

        private void WireUiSubscriptions(IObservable<long> totalDownloadedStream,
            IObservable<double> progressStream, IObservable<string> timeLeftStream, IObservable<bool> unzippingStream)
        {
            unzippingStream.ObserveOnDispatcher()
                .Subscribe(x =>
                {
                    canvas.Children.Clear();
                    canvas.Children.Add(new DownloadUpToDateUC(configs));
                });

            timeLeftStream.ObserveOnDispatcher()
                .Subscribe(x => { SetStatusInfo("TimeLeft", x); });

            totalDownloadedStream.ObserveOnDispatcher()
                .Subscribe(x => { }, x => { }, () => { SetStatusInfo("VerifyZipImage"); });

            progressStream.ObserveOnDispatcher().Subscribe(x =>
            {
                DownloadProgressBar.Value = x;
                ProgressBarTextBlock.Text = Math.Round(100*x, 2).ToString(CultureInfo.InvariantCulture) + "%";
            });
        }

        private void SetStatusInfo(string statusTextKeyInResource, string value = "")
        {
            if (value != "")
            {
                LocalizationManager.SetWatch(statusTextKeyInResource, DownloadStatusTextBlock, () => value);
            }
            else
            {
                LocalizationManager.SetWatch(statusTextKeyInResource, DownloadStatusTextBlock);
            }
        }

        private IObservable<bool> WireUnzippingStream(IObservable<long> totalDownloadedStream, string imageHash)
        {
            var unzippingStream =
                totalDownloadedStream
                    .Materialize()
                    .Where(n => n.Kind == NotificationKind.OnCompleted)
                    .Delay(TimeSpan.FromMilliseconds(500))
                    .SelectMany(
                        _ =>
                            FileVersionUtils.GetUnzipObservableAsync(configs.ImageMode, imageHash)
                                .Catch(Observable.Return(Tuple.Create(false, -1, (Int64) 0))))
                    .Publish();

            unzippingStream.Where(x => x.Item2 == -1).Delay(TimeSpan.FromMilliseconds(1050)).ObserveOnDispatcher().Subscribe(y =>
            {
                VerifyProgressTextBlock.Visibility = Visibility.Collapsed;
                ShowVerificationFailedFailure();
            });

            unzippingStream.Where(x => x.Item1 == false && x.Item2 != -1)
                .Scan(Tuple.Create((Int64) 0, (Int64) 0),
                    (acc, currentValue) => Tuple.Create(currentValue.Item2 + acc.Item1, currentValue.Item3))
                .Sample(TimeSpan.FromSeconds(1))
                .ObserveOnDispatcher()
                .Subscribe(y =>
                {
                    VerifyProgressTextBlock.Visibility = Visibility.Visible;
                    var progress = Math.Round(100*(1.0*y.Item1)/y.Item2, 2).ToString(CultureInfo.InvariantCulture) + "%";
                    VerifyProgressTextBlock.Text = progress;
                });
            unzippingStream.Connect();
            return unzippingStream.Where(x => x.Item1).Select(x => x.Item1);
        }

        private void ShowVerificationFailedFailure()
        {
            DownloadStatusTextBlock.Inlines.Clear();
            SetStatusInfo("ZipFileVerificationFailed");

            var hyperlink = new Hyperlink(new Run("\nLataa paketti uudelleen."));
            hyperlink.Click += (sender, e) =>
            {
                File.Delete(configs.ImageMode.ZipFilePath);
                StartDownload();
            };

            DownloadStatusTextBlock.Inlines.Add(hyperlink);
        }

        private static IObservable<double> WireProgressStream(IObservable<long> contentLengthStream,
            IObservable<long> totalDownloadedStream)
        {
            return contentLengthStream
                .CombineLatest(totalDownloadedStream, (total, loaded) => (1.0*loaded)/total);
        }

        private IObservable<long> WireTotalDownloadedStream()
        {
            var stream = HttpVersionUtils.GetImageBy(configs.ImageMode)
                .Scan(0L, (acc, x) => acc + x)
                .Sample(TimeSpan.FromSeconds(1)).Publish();
            stream.Connect();
            return stream;
        }

        private static IObservable<int> WireAverageDownloadStream(IObservable<long> totalDownloadedStream)
        {
            return
                totalDownloadedStream.Skip(1)
                    .Zip(totalDownloadedStream, (first, second) => (first - second))
                    .Buffer(5, 1)
                    .Select(x => (int) x.Average())
                    .Select(x => x - x%20000);
        }

        private static IObservable<string> WireTimeLeftStream(IObservable<long> contentLengthStream,
            IObservable<long> totalDownloadedStream)
        {
            var averageDl = WireAverageDownloadStream(totalDownloadedStream);
            return
                contentLengthStream.LastAsync()
                    .CombineLatest(totalDownloadedStream, (len, dl) => len - dl)
                    .Zip(averageDl.Where(x => x > 0), (left, speed) => left/speed)
                    .Select(x =>
                    {
                        var timeSpan = TimeSpan.FromSeconds(x);
                        return string.Format("{0:D2}h:{1:D2}m:{2:D2}s",
                            timeSpan.Hours,
                            timeSpan.Minutes,
                            timeSpan.Seconds);
                    });
        }

        private void RecheckDiskSpace(object sender, RoutedEventArgs e)
        {
            CheckDiskSpace();
        }
    }
}