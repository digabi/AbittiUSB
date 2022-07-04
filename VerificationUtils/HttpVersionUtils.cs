using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using NLog;

namespace Ytl.VerificationUtils
{
    public static class HttpVersionUtils
    {
        private const int BufferSize = 65536;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static Task<string> GetVersionHashAsync(string package)
        {
            return LatestVersionUtils.GetMd5ContentAsync(package, VerificationEnvironment.DigabiBaseUrl + "{1}/{0}.md5");
        }

        public static IObservable<long> GetImageBy(ImageMode imageMode)
        {
            return Observable.Create<long>(observer =>
            {
                DownloadImageAsync(imageMode, observer);
                return Disposable.Empty;
            });
        }

        public static IObservable<long> GetContentLength(ImageMode imageMode)
        {
            return GetContentLengthAsync(imageMode, LatestVersionUtils.GetLatestInfoAsync()).ToObservable();
        }

        private static async Task<long> GetContentLengthAsync(ImageMode imageMode, Task<int> latestVersion)
        {
            var request = new HttpRequestMessage(HttpMethod.Head, imageMode.GetDownloadAddressForLatestZippedImage(await latestVersion));
            using (var client = new HttpClient())
            {
                using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    return long.Parse(response.Content.Headers.First(h => h.Key.Equals("Content-Length")).Value.First());
                }
            }
        }

        private static async void DownloadImageAsync(ImageMode imageMode, IObserver<long> observer)
        {
            observer.OnNext(await GetFileSize(imageMode.ZipFilePath));
            DownloadImageAsync(imageMode, observer, 0);
        }

        private static async void DownloadImageAsync(ImageMode imageMode, IObserver<long> observer, int count)
        {
            var latest = await LatestVersionUtils.GetLatestInfoAsync();
            if (await GetFileSize(imageMode.ZipFilePath) == await GetContentLengthAsync(imageMode, LatestVersionUtils.GetLatestInfoAsync()))
            {
                observer.OnCompleted();
                return;
            }
            try
            {
                var downloadAddress = imageMode.GetDownloadAddressForLatestZippedImage(latest);
                await DownloadImage(imageMode.ZipFilePath, downloadAddress, observer);
            }
            catch (IOException)
            {
                if (count < 4)
                {
                    DownloadImageAsync(imageMode, observer, count + 1);
                }
                else
                {
                    throw;
                }
            }
        }

        private static async Task DownloadImage(string fileToSaveInto, string addressToDownload, IObserver<long> observer)
        {
            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Get, addressToDownload);
                request.Headers.Range = new RangeHeaderValue(await GetFileSize(fileToSaveInto), null);
                using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                using (var streamToReadFrom = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(fileToSaveInto, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    var buffer = new byte[BufferSize];
                    int read;
                    while ((read = await streamToReadFrom.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        observer.OnNext(read);
                        await fileStream.WriteAsync(buffer, 0, read);
                    }
                }
                await Task.Delay(200);
                observer.OnCompleted();
            }
        }

        private static Task<long> GetFileSize(string filename)
        {
            Logger.Info("Haetaan tiedostonkoko tiedostolle:" + filename);
            return Task.Factory.StartNew(() =>
            {
                var fInfo = new FileInfo(filename);
                return !fInfo.Exists ? 0 : fInfo.Length;
            });
        }

        internal static async Task<string> GetContentAsync(string latest)
        {
            Logger.Info("getting for:" + latest);
            using (var client = new HttpClient())
            using (var response = await client.GetAsync(latest))
            using (var content = response.Content)
            {
                return await content.ReadAsStringAsync();
            }
        }
    }
}