using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ytl.VerificationUtils
{
    public abstract class ImageMode
    {
        public abstract string Name { get; }
        public abstract string VerifyBurnOption { get; }
        public abstract string ZipFilePath { get; }
        public abstract string ImageFilePath { get; }
        public abstract string ZipFile { get; }
        public abstract string ImageFile { get; }
        public abstract string ImageFileMd5CalculatedCachePath { get; }
        public abstract string GetDownloadAddressForLatestZippedImage(int latest);
        public abstract Dictionary<string, string> ResourceKeys { get; }
    }

    public class Ktp : ImageMode
    {
        public override string Name
        {
            get { return "Ktp"; }
        }

        public override string VerifyBurnOption {
            get { return "VerifyServerBurn"; }
        }

        public override string ZipFilePath
        {
            get { return VerificationEnvironment.DirectoryPath + @"\ktp.zip"; }
        }

        public override string ImageFilePath
        {
            get { return VerificationEnvironment.DirectoryPath + @"\ktp.dd"; }
        }

        public override string ZipFile
        {
            get { return "ktp.zip"; }
        }

        public override string ImageFile
        {
            get { return "ktp.dd"; }
        }

        public override string ImageFileMd5CalculatedCachePath
        {
            get { return VerificationEnvironment.DirectoryPath + @"\ktp_calculated.md5"; }
        }

        public override string GetDownloadAddressForLatestZippedImage(int latest)
        {
            return VerificationEnvironment.DigabiBaseUrl + latest + "/ktp.zip";
        }

        public override Dictionary<string, string> ResourceKeys
        {
            get
            {
                return new Dictionary<string, string>
                {
                    {"HeaderForDownload", "HeaderForDownloadServer"},
                    {"DownloadNewVersion", "DownloadNewVersionServer"}
                };
            }
        }
    }

    public class Koe : ImageMode
    {
        public override string Name
        {
            get { return "Koe"; }
        }

        public override string VerifyBurnOption {
            get { return "VerifyStudentBurn"; }
        }
        
        public override string ZipFilePath
        {
            get { return VerificationEnvironment.DirectoryPath + @"\koe.zip"; }
        }

        public override string ImageFilePath
        {
            get { return VerificationEnvironment.DirectoryPath + @"\koe.dd"; }
        }

        public override string ZipFile
        {
            get { return "koe.zip"; }
        }

        public override string ImageFile
        {
            get { return "koe.dd"; }
        }

        public override string ImageFileMd5CalculatedCachePath
        {
            get { return VerificationEnvironment.DirectoryPath + @"\koe_calculated.md5"; }
        }

        public override string GetDownloadAddressForLatestZippedImage(int latest)
        {
            return VerificationEnvironment.DigabiBaseUrl + latest + "/koe.zip";
        }

        public override Dictionary<string, string> ResourceKeys
        {
            get
            {
                return new Dictionary<string, string>
                {
                   {"HeaderForDownload", "HeaderForDownloadExam"},
                   {"DownloadNewVersion", "DownloadNewVersionExam"}
                   
                };
            }
        }
    }
}
