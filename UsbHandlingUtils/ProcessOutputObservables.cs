using System;
using System.Diagnostics;
using System.Reactive.Linq;
using NLog;

namespace Ytl.UsbHandlingUtils
{
    internal class ProcessOutputObservables
    {
        private readonly IObservable<DataReceivedEventArgs> errorObservable;
        private readonly IObservable<Tuple<string, int>> exitObservable;
        private readonly IObservable<DataReceivedEventArgs> outputObservable;
        private readonly Process process;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private ProcessOutputObservables(Process process, IObservable<DataReceivedEventArgs> output,
            IObservable<DataReceivedEventArgs> error, IObservable<Tuple<string,int>> exit)
        {
            this.process = process;
            exitObservable = exit;
            errorObservable = error;
            outputObservable = output;
        }

        public IObservable<DataReceivedEventArgs> OutputObservable
        {
            get { return outputObservable; }
        }

        public IObservable<DataReceivedEventArgs> ErrorObservable
        {
            get { return errorObservable; }
        }

        public IObservable<Tuple<string, int>> ExitObservable
        {
            get { return exitObservable; }
        }

        public Process Process
        {
            get { return process; }
        }

        public static ProcessOutputObservables GetProcessWithObservables(string commandPath)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo(commandPath)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                },
                EnableRaisingEvents = true
            };
 
            var errorStream =
                Observable.FromEventPattern<DataReceivedEventArgs>(process, "ErrorDataReceived")
                    .Select(x => x.EventArgs);

            var exitStream =
                Observable.FromEventPattern<EventArgs>(process, "Exited")
                    .Select(x =>
                    {
                        Logger.Info("Process with id " + process.Id + " exited. File:" + commandPath);
                        return Tuple.Create(commandPath, process.Id);
                    }).Publish();

            var outputStream =
                Observable.FromEventPattern<DataReceivedEventArgs>(process, "OutputDataReceived")
                    .Select(x => x.EventArgs).TakeUntil(exitStream.Delay(TimeSpan.FromSeconds(1)));
            exitStream.Connect();
            return new ProcessOutputObservables(process, outputStream, errorStream, exitStream);
        }

        public void Start(string arguments)
        {
            Process.StartInfo.Arguments = arguments;
            Process.Start();
            Logger.Info("Process started with id:" + Process.Id);
            var result = ProcessWatcherConnection.AddProcess(Process.Id);   
            Logger.Info("Process lisätty tarkkailuun, tarkkailussa olevat prosessit:" + result);
            Process.BeginOutputReadLine();
            Process.BeginErrorReadLine();
        }


    }
}