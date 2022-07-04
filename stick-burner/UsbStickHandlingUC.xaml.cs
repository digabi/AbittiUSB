using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Analytics;
using NLog;
using Ytl.UsbHandlingUtils;
using Ytl.VerificationUtils;

namespace Ytl.AbittiUsb
{
    public partial class UsbStickHandlingUC
    {
        private const string UsbDriveSelected = "UsbDriveSelected";
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly QueueObservable<bool> queue = new QueueObservable<bool>();
        private const string UserNotificationTextBlock = "BurnErrorTextBlock";
        private const string BurningProgressBar = "BurningProgressBar";
        private const string BurningProgressText = "BurningProgressText";
        private const string BurningProgressGrid = "BurningProgressGrid";
        private ISubject<object, object> pollUsb = Subject.Synchronize(new Subject<object>());
        private Activity activity;

        public UsbStickHandlingUC(Activity activity)
        {
            this.activity = activity;

            InitializeComponent();
            WireUsbDetections();

            var usbSelectionsExists = queue.GetObservable().Publish();
            usbSelectionsExists.Connect();
            Observable.Interval(TimeSpan.FromSeconds(3))
                .StartWith(0)
                .SelectMany(x => VerificationUtils.VerificationUtils.IsCachedUpToDateObservable(new Koe()))
                .CombineLatest(usbSelectionsExists, (imageReady, selections) => imageReady && selections)
                .ObserveOnDispatcher()
                .Subscribe(x => { StudentBurnButton.IsEnabled = x; },
                           e => { throw e; },
                           () => { });

            Observable.Interval(TimeSpan.FromSeconds(3))
                .StartWith(0)
                .SelectMany(x => VerificationUtils.VerificationUtils.IsCachedUpToDateObservable(new Ktp()))
                .CombineLatest(usbSelectionsExists, (imageReady, selections) => imageReady && selections)
                .ObserveOnDispatcher()
                .Subscribe(x => { ExamBurnButton.IsEnabled = x; },
                           e => { throw e; },
                           () => { });
            queue.Notify(GetUsbDatas().Any(x => x.Selected));

            usbSelectionsExists
                .ObserveOnDispatcher().Subscribe(x => { FormatButton.IsEnabled = x; });
        }

        private void WireUsbDetections()
        {
            UsbDetectionObservable.GetUsbListObservable(pollUsb)
            .ObserveOnDispatcher()
            .Subscribe(newUsbDatas => {
                var nIdx = 0;
                var oIdx = 0;
                while (nIdx < newUsbDatas.Count && oIdx < UsbListBox.Items.Count) {
                    var o = UsbListBox.Items[oIdx] as UsbData;
                    var n = newUsbDatas[nIdx];
                    var c = string.Compare(o.PhysicalDrive, n.PhysicalDrive);
                    if (c < 0) {
                        UsbListBox.Items.RemoveAt(oIdx);
                    } else if (c > 0) {
                        UsbListBox.Items.Insert(oIdx, n);
                        ++oIdx;
                        ++nIdx;
                    } else {
                        if (UsbData.AreSame(o, n)) {
                            o.ExtendWithDetailsFrom(n);
                            ++nIdx;
                            ++oIdx;
                        } else {
                            UsbListBox.Items.RemoveAt(oIdx);
                            UsbListBox.Items.Insert(oIdx, n);
                            ++nIdx;
                        }
                    }
                }
                while (oIdx < UsbListBox.Items.Count)
                    UsbListBox.Items.RemoveAt(oIdx);
                while (nIdx < newUsbDatas.Count)
                    UsbListBox.Items.Insert(oIdx++, newUsbDatas[nIdx++]);
                UpdateSelectedCountText();
            });
        }

        private IEnumerable<UsbData> GetUsbDatas()
        {
            return UsbListBox.Items.Cast<UsbData>();
        }

        private async void FormatButton_Click(object sender, RoutedEventArgs e) {
            using (var act = activity.Start()) {
                var usbDatas = GetUsbTuples().Where(x => x.Item1.Selected).ToList();
                FormatButton.IsEnabled = false;
                var @select = usbDatas.Select(FormatDriveWithNotificationsAsync);
                await Task.WhenAll(@select.ToArray());
                pollUsb.OnNext(null);
            }
        }

        private async Task FormatDriveWithNotificationsAsync(Tuple<UsbData, ContentPresenter> x) {
            x.Item2.IsEnabled = false;

            Dispatcher.CurrentDispatcher.Invoke(() => {
                GetUiControl<CheckBox>(x.Item2, UsbDriveSelected).IsChecked = false;
                var uiControl = GetUiControl<TextBlock>(x.Item2, UserNotificationTextBlock);
                LocalizationManager.SetWatch("Formatting", uiControl);
                uiControl.Visibility = Visibility.Visible;

            });
            await Task.Factory.StartNew(() => {
                Logger.Info("Start formatting drive:" + x.Item1.Drive);
                x.Item1.Selected = false;
                FormatManager.FormatDrive(x.Item1.PhysicalDrive);
                Logger.Info("Drive formatted:" + x.Item1.Drive);
            });
            UpdateSelectedCountText();
            await AnalyticsProvider.TrackEvent(MainWindow.Environment, "disk-format", "formatted");
            x.Item2.IsEnabled = true;
            GetUiControl<TextBlock>(x.Item2, UserNotificationTextBlock).Visibility = Visibility.Collapsed;
        }

        private IEnumerable<Tuple<UsbData, ContentPresenter>> GetUsbTuples()
        {
            return GetUsbDatas()
                .Select(x =>
                {
                    var containerFromItem = UsbListBox.ItemContainerGenerator.ContainerFromItem(x);
                    return Tuple.Create(x, containerFromItem as ContentPresenter);
                });
        }

        private void StudentBurnButton_Click(object sender, RoutedEventArgs e)
        {
            BurnImageToUsb(new Koe());
        }

        private void ExamBurnButton_Click(object sender, RoutedEventArgs e)
        {
            BurnImageToUsb(new Ktp());
        }

        private void BurnImageToUsb(ImageMode imageMode) {
          var verify = (bool)Properties.Settings.Default[imageMode.VerifyBurnOption];

          BurnImageToUsb(imageMode.ImageFile, imageMode.Name, verify);
        }

        private void BurnImageToUsb(string imageFile, string imageName, bool verify)
        {
            var act = activity.Start();
            var imagePath = VerificationEnvironment.DirectoryPath + @"\" + imageFile;
            var imageSize = new FileInfo(imagePath).Length;
            var interval = Observable.Interval(TimeSpan.FromMilliseconds(400));
            var burnProgressObservable = GetUsbTuples().ToObservable()
                .Where(x => x.Item1.Selected)
                .Do(PrepareUiForBurning())
                .Zip(interval, (usb, ignored) => usb)
                .SelectMany(GetUsbBurningProgressObservable(imageFile, imageName, verify))
                .Finally(() => { act.Dispose(); pollUsb.OnNext(null); })
                .Publish();
            WireBurnSubscriptions(burnProgressObservable, verify, imageSize);
            burnProgressObservable.Connect();
        }

        private static void WireBurnSubscriptions(
            IObservable<Tuple<string, ContentPresenter, string, UsbData>> rawBurnObservable,
            bool verify,
            long imageSize)
        {
            Func<Tuple<string, ContentPresenter, string, UsbData>, bool> never = (x) => false;

            var isVerifyingObservable =
                rawBurnObservable.Scan(
                    never,
                    (isVerifying, usb) => {
                        if (!usb.Item1.StartsWith("Verifying")) {
                            return isVerifying;
                        } else {
                            var physicalDrive = usb.Item4.PhysicalDrive;
                            return (u) => u.Item4.PhysicalDrive == physicalDrive || isVerifying(u);
                        }
                    });

            var burnObservable =
                Observable.Zip(
                    rawBurnObservable,
                    isVerifyingObservable,
                    Tuple.Create);

            burnObservable
                .Where(x => x.Item1.Item1.EndsWith("%"))
                .ObserveOnDispatcher()
                .Subscribe(x =>
                {
                    var usb = x.Item1;
                    var isVerifying = x.Item2;

                    if (isVerifying(usb)) {
                        var diskPercentage = int.Parse(usb.Item1.Replace("%", ""));
                        var bytes = usb.Item4.DiskSize * diskPercentage / 100;
                        var imagePercentage = (int)(bytes * 100 / imageSize);
                        if (imagePercentage < 0)
                            imagePercentage = 0;
                        if (100 < imagePercentage)
                            imagePercentage = 100;
                        GetUiControl<ProgressBar>(usb.Item2, BurningProgressBar).Value = imagePercentage;
                        GetUiControl<TextBlock>(usb.Item2, BurningProgressText).Text =
                            imagePercentage.ToString() + "% " + LocalizationManager.GetLocalizedValue<string>("Verified");
                        GetUiControl<Grid>(usb.Item2, BurningProgressGrid).Visibility = Visibility.Visible;
                    } else {
                        GetUiControl<ProgressBar>(usb.Item2, BurningProgressBar).Value = int.Parse(usb.Item1.Replace("%", ""));
                        GetUiControl<TextBlock>(usb.Item2, BurningProgressText).Text =
                            usb.Item1 + " " + LocalizationManager.GetLocalizedValue<string>("Burned");
                        GetUiControl<Grid>(usb.Item2, BurningProgressGrid).Visibility = Visibility.Visible;
                    }
                });

            burnObservable
                .Where(x => x.Item1.Item1.StartsWith("Code ") || x.Item1.Item1.StartsWith("Error:"))
                .ObserveOnDispatcher()
                .Subscribe(x =>
                {
                    var usb = x.Item1;
                    var isVerifying = x.Item2;

                    NotifyAnalytics(usb, isVerifying(usb) ? "usb-fail-verify" : "usb-fail-write");
                    GetUiControl<CheckBox>(usb.Item2, UsbDriveSelected).IsEnabled = true;
                    LocalizationManager.SetWatch("CleanDiskFirst",
                        GetUiControl<TextBlock>(usb.Item2, UserNotificationTextBlock),
                        () => usb.Item1);
                    GetUiControl<TextBlock>(usb.Item2, UserNotificationTextBlock).Visibility = Visibility.Visible;
                    GetUiControl<Grid>(usb.Item2, BurningProgressGrid).Visibility = Visibility.Hidden;
                });

            burnObservable
                .Where(x => (!verify || x.Item2(x.Item1)) && x.Item1.Item1 == "ok")
                .ObserveOnDispatcher()
                .Subscribe(x =>
                {
                    var usb = x.Item1;
                    NotifyAnalytics(usb, "usb-burn");
                    GetUiControl<Grid>(usb.Item2, BurningProgressGrid).Visibility = Visibility.Hidden;
                    GetUiControl<CheckBox>(usb.Item2, UsbDriveSelected).IsEnabled = true;

                    var uiControl = GetUiControl<TextBlock>(usb.Item2, UserNotificationTextBlock);
                    uiControl.Text = "(" + usb.Item3 + " - ok)";
                    uiControl.Visibility = Visibility.Visible;

                });
        }

        private static async void NotifyAnalytics(Tuple<string, ContentPresenter, string, UsbData> usb, string action)
        {
            await AnalyticsProvider.TrackEvent(MainWindow.Environment, action, usb.Item3);
        }

        private static Func<Tuple<UsbData, ContentPresenter>, IObservable<Tuple<string, ContentPresenter, string, UsbData>>>
            GetUsbBurningProgressObservable(string imageFilePath, string imageName, bool verify)
        {
            return usb =>
            {
                return UsbDiskObservables.GetUsbDiskBurnObservable(usb.Item1.PhysicalDrive, imageFilePath, verify)
                    .Do(Logger.Info)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Select(x => Tuple.Create(x.Trim(), usb.Item2, imageName, usb.Item1));
            };
        }

        private static Action<Tuple<UsbData, ContentPresenter>> PrepareUiForBurning()
        {
            return usb =>
            {
                usb.Item1.Selected = false;
                var uiControl = GetUiControl<CheckBox>(usb.Item2, UsbDriveSelected);
                GetUiControl<ProgressBar>(usb.Item2, BurningProgressBar).Value = 0;
                GetUiControl<TextBlock>(usb.Item2, BurningProgressText).Text = "0%";
                GetUiControl<Grid>(usb.Item2, BurningProgressGrid).Visibility = Visibility.Visible;
                LocalizationManager.SetWatch("Empty",
                    GetUiControl<TextBlock>(usb.Item2, UserNotificationTextBlock));
                uiControl.IsEnabled = false;
                uiControl.IsChecked = false;
            };
        }

        private static T GetUiControl<T>(ContentPresenter contentPresenter, string componentName)
        {
            var myDataTemplate = contentPresenter.ContentTemplate;
            return (T) myDataTemplate.FindName(componentName, contentPresenter);
        }

        private void UpdateSelectedCountText()
        {
            LocalizationManager.SetWatch("NumberOfSelectedSticks",
                UsbMediaCountTextBlock,
                GetUsbDatas().Count(x => x.Selected).ToString,
                GetUsbDatas().Count(x => x.Selectable).ToString);
        }

        private void UsbDriveSelected_OnChecked(object sender, RoutedEventArgs e)
        {
            queue.Notify(GetUsbDatas().Any(x => x.Selected));
            UpdateSelectedCountText();
        }

        private void UsbDriveSelected_OnUnchecked(object sender, RoutedEventArgs e)
        {
            queue.Notify(GetUsbDatas().Any(x => x.Selected));
            UpdateSelectedCountText();
        }

        private void SelectAllMediasChecked(object sender, RoutedEventArgs e)
        {
            foreach (var usb in GetUsbTuples())
            {
                var uiControl = GetUiControl<CheckBox>(usb.Item2, UsbDriveSelected);
                usb.Item1.Selected = uiControl.IsEnabled;
                uiControl.IsChecked = uiControl.IsEnabled;
            }
            UpdateSelectedCountText();
        }

        private void SelectAllMediasUnChecked(object sender, RoutedEventArgs e)
        {
            foreach (var usb in GetUsbTuples())
            {
                usb.Item1.Selected = false;
                var uiControl = GetUiControl<CheckBox>(usb.Item2, UsbDriveSelected);
                uiControl.IsChecked = false;
            }
            UpdateSelectedCountText();
        }
    }
}