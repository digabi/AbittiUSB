using System.Windows;
using System.Windows.Media;
using Ytl.VerificationUtils;
using System;
using System.Configuration;
using System.Globalization;
using System.Reactive.Threading.Tasks;
using System.Reflection.Emit;
using System.Management;
using System.Security.Principal;
using System.Reflection;
using System.Threading;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Controls;
using WPFLocalizeExtension.Engine;
using WPFLocalizeExtension.Providers;

namespace Ytl.AbittiUsb
{
    public sealed class Activity {
        private IObserver<bool> observer;

        public Activity(IObserver<bool> observer) {
            this.observer = observer;
        }

        public IDisposable Start() {
            observer.OnNext(true);
            return new Stopper(observer);
        }

        private sealed class Stopper : IDisposable {
            private IObserver<bool> observer;

            internal Stopper(IObserver<bool> observer) {
                this.observer = observer;
            }

            ~Stopper() {
                Dispose();
            }

            public void Dispose() {
                GC.SuppressFinalize(this);
                var observer = System.Threading.Interlocked.Exchange(ref this.observer, null);
                if (null != observer)
                    observer.OnNext(false);
            }
        }
    }

    public partial class MainWindow
    {
        public static string Environment = ConfigurationManager.AppSettings["Environment"];
        private ISubject<bool, bool> activitySubject = Subject.Synchronize(new Subject<bool>());
        private Activity activity;

        public MainWindow() {
            if (!App.IsRunAsAdministrator()) {
                return;
            }
            InitializeComponent();

            activity = new Activity(activitySubject);
            activitySubject
                .Scan(0, (n, on) => on ? n+1 : n-1)
                .Select(n => n > 0)
                .DistinctUntilChanged()
                .ObserveOnDispatcher()
                .Subscribe(active => Settings.IsEnabled = !active);

            this.SetValue(ResxLocalizationProvider.DefaultAssemblyProperty,
                typeof(MainWindow).Assembly.GetName().Name);

            Analytics.AnalyticsProvider.TrackEvent(Environment, "start", "main-window");

            SetStickCanvases();

            UsbGrid.Children.Clear();
            var shuc = new UsbStickHandlingUC(activity);
            UsbGrid.Children.Add(shuc);
            Grid.SetColumn(shuc, 0);
            Grid.SetRow(shuc, 0);

            LocalizationManager.SetWatch(
                "WindowTitle",
                this,
                new Func<string>[] { () => ConfigurationManager.AppSettings["ProductVersion"] },
                (window, title) => window.Title = title);

            SetLanguage(Properties.Settings.Default.Language);
        }

        private void SetStickCanvases() {
            LeftCanvas.Children.Clear();
            LeftCanvas.Children.Add(new VerificationInProgress(new DownloadConfigs(new Koe()), LeftCanvas, activity));

            RightCanvas.Children.Clear();
            RightCanvas.Children.Add(new VerificationInProgress(new DownloadConfigs(new Ktp()), RightCanvas, activity));
        }

        private void Settings_Click(object sender, System.Windows.RoutedEventArgs e) {
            if (SettingsDialog.ShowModally())
                SetStickCanvases();
        }

        private void Finnish_Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            SetLanguage("fi-FI");
        }

        private void Sweden_Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            SetLanguage("sv-SE");
        }

        private void SetLanguage(string lang) {
            var langKey = lang.Replace('-', '_');
            foreach (var block in LanguageLinks.Children.OfType<System.Windows.Controls.TextBlock>())
                foreach (var link in block.Inlines.OfType<System.Windows.Documents.Hyperlink>())
                    link.IsEnabled = block.Name != langKey;
            LocalizeDictionary.Instance.SetCurrentThreadCulture = true;
            LocalizeDictionary.Instance.Culture = new CultureInfo(lang);
            LocalizationManager.UpdateFieldsAfterLanguageChange();
            Properties.Settings.Default.Language = lang;
            Properties.Settings.Default.Save();
        }
    }
}
