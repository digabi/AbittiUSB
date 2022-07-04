using System;
using System.IO;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WPFLocalizeExtension.Engine;
using WPFLocalizeExtension.Providers;
using Ytl.VerificationUtils;

namespace Ytl.AbittiUsb {
    /// <summary>
    /// Interaction logic for SettingsDialog.xaml
    /// </summary>
    public partial class SettingsDialog : Window {
        public SettingsDialog() {
            InitializeComponent();

            LocalizationManager.SetWatch("SettingsTitle", this);
            LocalizationManager.SetWatch("DiskImageFolder", DiskImageFolderLabel);
            LocalizationManager.SetWatch("DiskImageFolderBrowse", DiskImageFolderBrowse);
            LocalizationManager.SetWatch("DiskImageFolderReset", DiskImageFolderReset);
            LocalizationManager.SetWatch("SettingsSave", Save);
            LocalizationManager.SetWatch("SettingsCancel", Cancel);
            LocalizationManager.SetWatch("VerifyServerBurn", VerifyServerBurnText);
            LocalizationManager.SetWatch("VerifyStudentBurn", VerifyStudentBurnText);

            DiskImageFolder.IsEnabled = false;

            DiskImageFolder.TextChanged += (s, e) =>
                DiskImageFolderReset.IsEnabled = DiskImageFolder.Text != GetDefaultDiskImageFolder();

            VerifyStudentBurn.IsChecked = Properties.Settings.Default.VerifyStudentBurn;
            VerifyServerBurn.IsChecked = Properties.Settings.Default.VerifyServerBurn;

            DiskImageFolder.Text = Properties.Settings.Default.DigabiFolder;
        }

        public static bool ShowModally() {
            var dialog = new SettingsDialog();
            var window = Application.Current.MainWindow;
            if (null != window)
                dialog.Owner = window;
            var save = dialog.ShowDialog() ?? false;
            if (save)
                dialog.SaveSettings();
            return save;
        }

        public static string GetDefaultDiskImageFolder() {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\" + ConfigurationManager.AppSettings["DigabiFolder"];
        }

        private void SaveSettings() {
            var newFolder = DiskImageFolder.Text;
            var oldFolder = Properties.Settings.Default.DigabiFolder;

            Directory.CreateDirectory(newFolder);
            Properties.Settings.Default.DigabiFolder = newFolder;
            DiskImageFolder.Text = Properties.Settings.Default.DigabiFolder;
            VerificationEnvironment.DirectoryPath = Properties.Settings.Default.DigabiFolder;

            Properties.Settings.Default.VerifyStudentBurn = VerifyStudentBurn.IsChecked ?? Properties.Settings.Default.VerifyStudentBurn;
            Properties.Settings.Default.VerifyServerBurn = VerifyServerBurn.IsChecked ?? Properties.Settings.Default.VerifyServerBurn;

            Ytl.AbittiUsb.Properties.Settings.Default.Save();
        }

        private void DiskImageFolderBrowse_Click(object sender, RoutedEventArgs e) {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog()) {
                dialog.SelectedPath = Properties.Settings.Default.DigabiFolder;
                var result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK) {
                    DiskImageFolder.Text = dialog.SelectedPath;
                }
            }
        }

        private void DiskImageFolderReset_Click(object sender, RoutedEventArgs e) {
            DiskImageFolder.Text = GetDefaultDiskImageFolder();
        }

        private void Save_Click(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }
    }
}
