using System;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Analytics;
using NLog;

namespace Ytl.VerificationUtils
{
    public class FileVersionUtils
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly string Environment = ConfigurationManager.AppSettings["Environment"];

        public static IObservable<Tuple<bool, int, Int64>> GetUnzipObservableAsync(ImageMode mode, string imageHash)
        {
            return Observable.Create<Tuple<bool, int, Int64>>(observer =>
            {
                UnzipWithVerificationAsync(mode, imageHash, observer);
                return Disposable.Empty;
            });
        }

        private static async void UnzipWithVerificationAsync(ImageMode mode, string imageHash,
            IObserver<Tuple<bool, int, Int64>> observer)
        {
            Logger.Info("Puretaan tiedosto:" + mode.ZipFilePath + ". Odotettu tarkiste:" + imageHash);
            try
            {
                using (var archive = ZipFile.OpenRead(mode.ZipFilePath))
                {
                    var entry = archive.Entries.First(entryInZip => entryInZip.FullName.EndsWith(".dd"));

                    var buffer = new byte[1024*1024];
                    var length = entry.Length;
                    using (var stream = entry.Open())
                    using (var hashAlgorithm = MD5.Create())
                    using (var fs = new FileStream(mode.ImageFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                    {
                        int read;
                        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fs.WriteAsync(buffer, 0, read);
                            observer.OnNext(Tuple.Create(false, read, length));
                            hashAlgorithm.TransformBlock(buffer, 0, read, buffer, 0);
                        }
                        File.SetCreationTime(mode.ImageFilePath, entry.LastWriteTime.DateTime);
                        hashAlgorithm.TransformFinalBlock(buffer, 0, 0);
                        var hash = GetHashAsString(hashAlgorithm.Hash);
                        if (string.Equals(hash, imageHash, StringComparison.OrdinalIgnoreCase))
                        {
                            Logger.Info("Tarkisteet ovat samat:" + hash + " ja " + imageHash);
                            File.WriteAllText(mode.ImageFileMd5CalculatedCachePath, hash);
                        }
                        else
                        {
                            Logger.Info("Tarkisteet eivät ole samat:" + hash + " ja " + imageHash);
                            throw new Exception("Hashes do not match " + hash + " " + imageHash);
                        }                  
                    }
                }
                Logger.Info("Tiedosto purettu:" + mode.ZipFilePath);    
                observer.OnNext(Tuple.Create(true, 0, (Int64)0));
                observer.OnCompleted();
            }
            catch (Exception e)
            {
                Logger.Info("Tiedoston purkaminen epäonnistui:");
                Logger.Error(e);
                observer.OnError(e);
            }
        }

        public static void CreateFolderForApplication()
        {
            if (!Directory.Exists(VerificationEnvironment.DirectoryPath))
            {
                Directory.CreateDirectory(VerificationEnvironment.DirectoryPath);
            }
        }

        public static IObservable<string> CalculateMd5ForFileAsync(string filename)
        {
            return Observable.Start(() =>
            {
                Logger.Info("Lasketaan hash tiedostolle:" + filename);
                if (!File.Exists(filename)) return Guid.NewGuid().ToByteArray();
                return Retry(GetMd5Hash, filename, TimeSpan.FromSeconds(2), 50);
            }).Select(GetHashAsString);
        }

        private static T Retry<T>(Func<string, T> action, string param, TimeSpan retryInterval, int retryCount)
        {
            var retry = retryCount;
            while (retry > 0)
            {
                try
                {
                    return action(param);
                }
                catch (IOException ex)
                {
                    if (retry == retryCount)
                    {
                        AnalyticsProvider.TrackEvent(Environment, "file-locked", "retrying");
                    }
                    Logger.Info("Tiedosto on lukossa? retry counter:" + retry + "virhe:" + ex.Message);
                    retry = retry - 1;
                    if (retry == 0)
                    {
                        AnalyticsProvider.TrackEvent(Environment, "file-locked", "retrying-failed");
                        throw;
                    }
                    Thread.Sleep(retryInterval);
                    Logger.Info("Yritetään uudelleen");
                }
            }
            throw new Exception("invalid condition!");
        }

        private static byte[] GetMd5Hash(string filename)
        {
            Logger.Info("Starting hash:" + filename);
            using (var hashAlgorithm = MD5.Create())
            using (var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var buffer = new byte[262144];
                int read;
                while ((read = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    hashAlgorithm.TransformBlock(buffer, 0, read, buffer, 0);
                }
                hashAlgorithm.TransformFinalBlock(buffer, 0, 0);
                Logger.Info("Ending hash:" + filename);
                return hashAlgorithm.Hash;
            }
        }

        public static async Task<string> GetMd5CachedHash(ImageMode imageMode)
        {
            var filename = imageMode.ImageFileMd5CalculatedCachePath;
            if (!File.Exists(filename)) return Guid.NewGuid().ToString();

            var count = 0;
            while (true)
            {
                try
                {
                    await Task.Delay(count*500);
                    using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (var streamReader = new StreamReader(fs))
                        {
                            if (count > 0)
                            {
                                await AnalyticsProvider.TrackEvent(Environment, "file-locked", "retrying-md5-helped");
                            }
                            return await streamReader.ReadToEndAsync();
                        }
                    }
                }
                catch (IOException ex)
                {
                    Logger.Info("Tiedosto on lukossa? yrityskerta:" + count + "virhe:" + ex.Message);

                    if (count > 5)
                    {
                        throw;
                    }
                    count++;
                }
            }
        }

        public static string GetFileSize(string filename, string byteSuffix)
        {
            double len = new FileInfo(filename).Length;
            return FormatFileSizeToHumanReadable(len, byteSuffix);
        }

        public static string FormatFileSizeToHumanReadable(double len, string byteSuffix)
        {
            string[] sizes = {"", "K", "M", "G"};
            var order = 0;
            while (len >= 1024 && order + 1 < sizes.Length)
            {
                order++;
                len = len/1024;
            }
            return String.Format("{0:0.##} {1}{2}", len, sizes[order], byteSuffix);
        }

        public static DateTime GetFileCreationDate(string filename)
        {
            return File.GetCreationTime(filename);
        }

        private static string GetHashAsString(byte[] array)
        {
            return String.Join("", array.Select(x => String.Format("{0:X2}", x)));
        }
    }
}