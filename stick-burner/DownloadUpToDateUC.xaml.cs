using System.IO;
using System.Threading.Tasks;
using Ytl.VerificationUtils;

namespace Ytl.AbittiUsb
{
    public partial class DownloadUpToDateUC
    {
        public DownloadUpToDateUC(DownloadConfigs configs)
        {
            InitializeComponent();
            LocalizationManager.SetWatch(configs.GetResourceKey("HeaderForDownload"), HeaderTextBlock);
            SetImageFileInfoText(configs.ImageMode);
            File.Delete(configs.ImageMode.ZipFilePath);
        }

        private void SetImageFileInfoText(ImageMode imageMode)
        {
            var fileDate = FileVersionUtils.GetFileCreationDate(imageMode.ImageFilePath);
            LocalizationManager.SetWatch("ImageFileInfo",
                ImageFileInfo,
                () => {
                    var byteSuffix = LocalizationManager.GetLocalizedValue<string>("ByteSuffix");
                    return FileVersionUtils.GetFileSize(imageMode.ImageFilePath, byteSuffix);
                },
                () => fileDate.ToString("d"));
        }
    }
}