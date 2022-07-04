using System.IO;
using System.IO.Pipes;
using NLog;

namespace Ytl.UsbHandlingUtils
{
    public static class ProcessWatcherConnection
    {
        public const string AddingPipe = "AbittiUSBPipeAdd";
        public const string DeletingPipe = "AbittiUSBPipeDelete";

        internal static string AddProcess(int processId)
        {
            return SendReceiveProcessToWatcher(processId, AddingPipe);
        }

        internal static string DeleteProcess(int processId)
        {
            return SendReceiveProcessToWatcher(processId, DeletingPipe);
        }

        private static string SendReceiveProcessToWatcher(int processId, string pipe)
        {
            using (var add = new NamedPipeClientStream(pipe))
            {
                add.Connect();
                using (var streamReader = new StreamReader(add))
                using (var writer = new StreamWriter(add))
                {
                    writer.WriteLine(processId.ToString());
                    writer.Flush();
                    return streamReader.ReadLine();           
                }
            }
        }
    }
}