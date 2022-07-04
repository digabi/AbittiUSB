using Ytl.VerificationUtils;

namespace Ytl.AbittiUsb
{
    public class DownloadConfigs
    {
        private readonly ImageMode imageMode;

        public DownloadConfigs(ImageMode imageMode)
        {
            this.imageMode = imageMode;
        }

        public ImageMode ImageMode
        {
            get { return imageMode; }
        }

        public string GetResourceKey(string key)
        {
            string t;
            imageMode.ResourceKeys.TryGetValue(key, out t);
            return t;
        }
    }
}