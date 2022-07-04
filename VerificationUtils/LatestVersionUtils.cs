using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace Ytl.VerificationUtils
{
    internal class LatestVersionUtils
    {
        private static string LatestInfoUrl = VerificationEnvironment.DigabiBaseUrl + "latest.txt";
        private static readonly SemaphoreSlim LatestThrottler = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim Md5Throttler = new SemaphoreSlim(1, 1);

        private static readonly ConcurrentDictionary<string, string> VersionMd5Dictionary =
            new ConcurrentDictionary<string, string>();

        private static int LatestVersion { get; set; }

        internal static async Task<int> GetLatestInfoAsync()
        {
            await LatestThrottler.WaitAsync();
            try {
                if (LatestVersion == 0) {
                    LatestVersion = Int32.Parse(await HttpVersionUtils.GetContentAsync(LatestInfoUrl));
                }
            } finally {
                LatestThrottler.Release();
            }
            return LatestVersion;
        }

        internal static async Task<string> GetMd5ContentAsync(string packageName, string httpAbittiDigabiFiDdMd5)
        {
            await Md5Throttler.WaitAsync();
            try {
                if (!VersionMd5Dictionary.ContainsKey(packageName)) {
                    var latestVersion = await GetLatestInfoAsync();
                    var page = String.Format(httpAbittiDigabiFiDdMd5, packageName, latestVersion);
                    VersionMd5Dictionary.GetOrAdd(packageName, (await HttpVersionUtils.GetContentAsync(page)).Split(' ').First());
                }
            } finally {
                Md5Throttler.Release();
            }
            string returnValue;
            VersionMd5Dictionary.TryGetValue(packageName, out returnValue);
            return returnValue;
        }
    }
}