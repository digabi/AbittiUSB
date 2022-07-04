using System;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace Ytl.VerificationUtils
{
    public class VerificationUtils
    {
        public static IObservable<bool> IsCachedUpToDateObservable(ImageMode imageMode)
        {
            return
                CachedUpToDateObservable(imageMode)
                    .Select(tuple => File.Exists(imageMode.ImageFilePath) && tuple.Item1.ToLower().Equals(tuple.Item2.ToLower()));
        }

        private static IObservable<Tuple<string, string>> CachedUpToDateObservable(ImageMode imageMode)
        {
            var cacheHash = FileVersionUtils.GetMd5CachedHash(imageMode).ToObservable();
            var httpHash = HttpVersionUtils.GetVersionHashAsync(imageMode.ImageFile).ToObservable();
            return cacheHash.CombineLatest<string, string, Tuple<string, string>>(httpHash, Tuple.Create);
        }

        public static IObservable<bool> GetHashVerificationObservableForZipFile(ImageMode imageMode)
        {
            var fileHash = FileVersionUtils.CalculateMd5ForFileAsync(imageMode.ZipFilePath);
            var httpHash = HttpVersionUtils.GetVersionHashAsync(imageMode.ZipFile).ToObservable();
            return fileHash.CombineLatest(httpHash, (file, http) => http.ToLower().Equals(file.ToLower()));
        }
    }
}