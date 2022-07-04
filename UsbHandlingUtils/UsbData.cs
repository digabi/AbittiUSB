namespace Ytl.UsbHandlingUtils
{
    using System.ComponentModel;

    public class UsbData : INotifyPropertyChanged
    {
        private bool selected;

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyChanged(string info) {
            if (PropertyChanged != null)
              PropertyChanged(this, new PropertyChangedEventArgs(info));
        }

        public UsbData(string name, string serial, string drive, string physicalDrive, long diskSize)
        {
            Name = name;
            Serial = serial;
            Drive = drive;
            PhysicalDrive = physicalDrive;
            DiskSize = diskSize;
            Selectable = diskSize >= (long) (3.5*1024*1024*1024);
        }

        private string name;
        private string serial;
        private string drive;
        private string physicalDrive;
        private long diskSize;
        private bool selectable;

        public string Name {
          get { return name; }
          private set { name = value; NotifyChanged("Name"); } }
        public string Serial {
          get { return serial; }
          private set { serial = value; NotifyChanged("Serial"); } }
        public string Drive {
          get { return drive; }
          private set { drive = value; NotifyChanged("Drive"); } }
        public string PhysicalDrive {
          get { return physicalDrive; }
          private set { physicalDrive = value; NotifyChanged("PhysicalDrive"); } }
        public long DiskSize {
            get { return diskSize; }
            private set { diskSize = value; NotifyChanged("DiskSize"); } }
        public bool Selectable {
          get { return selectable; }
          set { selectable = value; NotifyChanged("Selectable"); } }

        public static bool AreSame(UsbData lhs, UsbData rhs) {
            return lhs.Serial == rhs.Serial &&
                   lhs.PhysicalDrive == rhs.PhysicalDrive;
        }

        public void ExtendWithDetailsFrom(UsbData that) {
            this.Drive = that.Drive;
        }

        public bool Selected
        {
            get { return Selectable && selected; }
            set { selected = value; }
        }
    }
}