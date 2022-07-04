using System;
using System.IO;
using System.Reactive.Linq;
using NLog;
using Ytl.VerificationUtils;

namespace Ytl.UsbHandlingUtils
{
    public class UsbDiskObservables
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static IObservable<string> GetUsbDiskBurnObservable(
            string driveNumber, string imageFilePath, bool verify)
        {
            var process = GetUsbWriterWithCleanup();
            var usbDataObservable = process.OutputObservable.Select(x => x.Data);
            var imagePath = VerificationEnvironment.DirectoryPath + @"\" + imageFilePath;
            var arguments = "r " + driveNumber + " \"" + imagePath + "\" /p /d /s" + (verify ? " /v" : "");

            return Observable.Create<string>(observer => {
                var dispose = usbDataObservable.Subscribe(observer);
                Logger.Info("Starting burning process with arguments:" + arguments);
                process.Start(arguments);
                return dispose;
            });
        }

        private static ProcessOutputObservables GetUsbWriterWithCleanup()
        {
            var tempFileName = Path.GetTempFileName();
            File.WriteAllBytes(tempFileName, Resources.Resources.usbitcmd);
            var process = ProcessOutputObservables.GetProcessWithObservables(tempFileName);
            process.ExitObservable.Delay(TimeSpan.FromSeconds(1)).Subscribe(x =>
            {
                File.Delete(x.Item1);
                var result = ProcessWatcherConnection.DeleteProcess(x.Item2);
                Logger.Info("Prosessi (id)" + x.Item2 + " poistettu tarkkailusta, tarkkailtavat prosessit:" + result);
            });
            return process;
        }
    }
}