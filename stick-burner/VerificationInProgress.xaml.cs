using System;
using System.Reactive.Linq;
using System.Windows.Controls;
using NLog;

namespace Ytl.AbittiUsb
{
    public partial class VerificationInProgress
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public VerificationInProgress(DownloadConfigs configs, Panel canvas, Activity activity)
        {
            InitializeComponent();
            var act = activity.Start();
            VerificationUtils.VerificationUtils.IsCachedUpToDateObservable(configs.ImageMode)
            .ObserveOnDispatcher()
            .Subscribe(
                isUpToDate => {
                    if (isUpToDate) {
                        Logger.Info("Ajantasalla oleva image:" + configs.ImageMode.GetType());
                        canvas.Children.Clear();
                        canvas.Children.Add(new DownloadUpToDateUC(configs));
                        act.Dispose();
                    } else {
                        Logger.Info("Image ei ollut ajantasalla:" + configs.ImageMode.GetType());
                        canvas.Children.Clear();
                        canvas.Children.Add(new DownloadNewVersionUC(configs, canvas, activity));
                        act.Dispose();
                    }
                },
                e => { throw e; },
                () => { });
        }
    }
}