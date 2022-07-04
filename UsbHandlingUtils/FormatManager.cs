using System;
using System.Diagnostics;
using NLog;

namespace Ytl.UsbHandlingUtils
{
    public sealed class FormatManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static void FormatDrive(string disk)
        {
            UnMount(disk);
            var psi = new ProcessStartInfo
            {
                FileName = "diskpart.exe",
                WorkingDirectory = Environment.SystemDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true
            };

            var formatProcess = Process.Start(psi);

            if (formatProcess == null) throw new Exception("process was null");
            var swStandardInput = formatProcess.StandardInput;

            swStandardInput.WriteLine("select disk " + disk);
            swStandardInput.WriteLine("clean");
            swStandardInput.WriteLine("create partition primary");
            swStandardInput.WriteLine("select partition 1");
            swStandardInput.WriteLine("format fs=fat32 quick");
            swStandardInput.WriteLine("active");
            swStandardInput.WriteLine("assign");
            swStandardInput.WriteLine("exit");
            var output = formatProcess.StandardOutput.ReadToEnd();
            formatProcess.WaitForExit();
            Logger.Info(output);
        }

        private static void UnMount(string disk)
        {
            var physicalDrive = UsbDetectionObservable.Physicaldrive + disk;
            var drive = UsbDetectionObservable.GetDriveLetter(physicalDrive);
            if (string.IsNullOrEmpty(drive)) {
                Logger.Info("Couldn't get drive for physical disk:" + disk);
                return;
            }
            Logger.Info("Mapped physical disk:" + disk + " to drive:" + drive);
            var psi = new ProcessStartInfo
            {
                FileName = "mountvol.exe",
                Arguments = drive + " /d",
                WorkingDirectory = Environment.SystemDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true
            };

            var unmountProcess = Process.Start(psi);
            var output = unmountProcess.StandardOutput.ReadToEnd();
            unmountProcess.WaitForExit();
            Logger.Info(output);
        }
    }
}