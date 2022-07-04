using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;
using Analytics;
using NLog;
using Ytl.UsbHandlingUtils;
using Ytl.VerificationUtils;

namespace Ytl.AbittiUsb
{
    public partial class App
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        protected override void OnStartup(StartupEventArgs e)
        {
            if (!IsRunAsAdministrator())
            {
                Logger.Info("Ei admin oikeuksia, yritetään sovelluksen uudelleenkäynnistystä.");
                var processInfo = new ProcessStartInfo(Assembly.GetExecutingAssembly().CodeBase)
                {
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    Verb = "runas"
                };

                try
                {
                    Process.Start(processInfo);
                }
                catch (Exception)
                {
                    Logger.Error("Sovellus vaatii admin oikeudet");
                    // The user did not allow the application to run as administrator
                    MessageBox.Show(
                        "AbittiUSB vaatii admin-oikeudet, ks. lisätietoja http://www.abitti.fi\n" +
                        "AbittiUSB kräver adminrättigheter. Läs mer på http://www.abitti.fi");
                }

                Current.Shutdown();
            }
            else
            {
                var uninstallSetupInfo =
                    UninstallUtils.tryGetUninstallSetupInfo(SettingsDialog.GetDefaultDiskImageFolder(), ConfigurationManager.AppSettings["ProductName"]);
                if (null != uninstallSetupInfo)
                    UninstallUtils.setupUninstall(uninstallSetupInfo.Value);

                if (string.IsNullOrEmpty(Ytl.AbittiUsb.Properties.Settings.Default.DigabiFolder)) {
                    Ytl.AbittiUsb.Properties.Settings.Default.DigabiFolder =
                        SettingsDialog.GetDefaultDiskImageFolder();
                    Ytl.AbittiUsb.Properties.Settings.Default.Save();
                }

                VerificationEnvironment.DirectoryPath = Ytl.AbittiUsb.Properties.Settings.Default.DigabiFolder;

                FileVersionUtils.CreateFolderForApplication();
                StartProcessWatcher();
                Logger.Info("Sovellus käynnistyy... admin oikeudet ok.");
                Current.DispatcherUnhandledException += App_DispatcherUnhandledException;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                base.OnStartup(e);
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            HandleExceptionAndExit((Exception) e.ExceptionObject);
        }

        private static void HandleExceptionAndExit(Exception ex)
        {
            Logger.Error(ex);
            if (AbittiUsb.MainWindow.Environment != "test")
            {
                AnalyticsProvider.TrackException(ex);
            }

            try
            {
                if (ex is HttpRequestException)
                {
                    MessageBox.Show(LocalizationManager.GetLocalizedValue<string>("NetworkUnavailable"));
                }
                else
                {
                    MessageBox.Show(LocalizationManager.GetLocalizedValue<string>("UnexpectedError"));
                }
            }
            finally
            {
                Process.GetCurrentProcess().Kill();
            }
        }

        private static void StartProcessWatcher()
        {
            var assemblyInfo = Assembly.GetExecutingAssembly();
            var assemblyLocation = Path.GetDirectoryName(assemblyInfo.Location);
            var arguments = "" + Process.GetCurrentProcess().Id + " " + ProcessWatcherConnection.AddingPipe + " " +
                            ProcessWatcherConnection.DeletingPipe;
            var watcherProcess = new Process
            {
                StartInfo =
                    new ProcessStartInfo(assemblyLocation + @"\ChildProcessUtil_1_0_7.exe",
                        arguments)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    }
            };
            watcherProcess.Start();
        }

        private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            HandleExceptionAndExit(e.Exception);
        }

        internal static bool IsRunAsAdministrator()
        {
            var wi = WindowsIdentity.GetCurrent();
            var wp = new WindowsPrincipal(wi);

            return wp.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}