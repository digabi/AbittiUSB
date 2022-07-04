using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Ytl.UsbHandlingUtils
{
    public static class Null {
        public static Y Bind<Y, X>(X x, Func<X, Y> x2y) where X: class where Y: class {
            return x == null ? null : x2y(x);
        }

        public static Y Bind<Y, X>(X? x, Func<X, Y> x2y) where X: struct where Y: class {
            return x == null ? null : x2y(x.Value);
        }

        public static Y? Bind<Y, X>(X x, Func<X, Y?> x2y) where X: class where Y: struct {
            return x == null ? null : x2y(x);
        }

        public static Y? Bind<Y, X>(X? x, Func<X, Y?> x2y) where X: struct where Y: struct {
            return x == null ? null : x2y(x.Value);
        }

        public static X? Return<X>(X x) where X: struct { return x; }

        public static Y Branch<Y, X>(X x, Func<X, Y> x2y, Func<Y> n2y) where X : class {
            return x == null ? n2y() : x2y(x);
        }
    }

    public class UsbDetectionObservable
    {
        internal const string Physicaldrive = @"\\.\PHYSICALDRIVE";

        private static IObservable<object> GetUsbEventObservable(string eventTable) {
            var query =
                new WqlEventQuery(
                    "SELECT * FROM " + eventTable + " WITHIN 2 WHERE TargetInstance ISA 'Win32_DiskDrive' and TargetInstance.InterfaceType='USB'");
            var watcher = new ManagementEventWatcher(query);
            watcher.Start();
            return Observable.FromEventPattern<EventArrivedEventArgs>(watcher, "EventArrived")
                    .Select(x => (ManagementBaseObject)x.EventArgs.NewEvent["TargetInstance"])
                    .Where(x => !ManagedObjectIsHardDisk(x))
                    .Select(_ => (object)null);
        }

        public static IObservable<List<UsbData>> GetUsbListObservable(IObservable<object> forcePoll) {
            return Observable.Merge(
                new IObservable<object>[] {
                    Observable.Return<object>(null),
                    forcePoll.Delay(TimeSpan.FromMilliseconds(750)),
                    GetUsbEventObservable("__InstanceCreationEvent"),
                    GetUsbEventObservable("__InstanceDeletionEvent")
                })
                .Throttle(TimeSpan.FromMilliseconds(200))
                .Select(_ => GetUsbList());
        }

        private static List<UsbData> GetUsbList() {
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive WHERE InterfaceType='USB'"))
                return searcher.Get()
                       .Cast<ManagementBaseObject>()
                       .Where(mboOpt =>
                           Null.Bind(mboOpt, mbo =>
                           Null.Bind(mbo["MediaType"] as string, mediaType =>
                           Null.Return(mediaType.ToLower().StartsWith("removable media")))) ?? false)
                       .SelectMany(mbo => {
                           try {
                               return new UsbData[] { MapUsbDriveManagedObjectToUsbData(mbo) };
                           } catch (IOException) {
                               return new UsbData[0];
                           }
                       })
                       .OrderBy(d => d.PhysicalDrive)
                       .ToList();
        }

        private static UsbData MapUsbDriveManagedObjectToUsbData(ManagementBaseObject instance)
        {
            return Null.Bind(instance["DeviceID"] as string, physicalDrive =>
                   Null.Bind(GetDriveLetter(physicalDrive), drive =>
                   Null.Bind(instance["PNPDeviceID"] as string, deviceId =>
                   Null.Bind(GetSerialFromDevice(deviceId), serial =>
                   Null.Bind(instance["Caption"] as string, deviceName =>
                   Null.Bind(instance["Size"], diskSize =>
                   new UsbData(deviceName, serial, string.IsNullOrEmpty(drive) ? "" : drive.First().ToString(),
                       physicalDrive.Substring(Physicaldrive.Length), (long)(UInt64)diskSize)))))));
        }

        private static string GetSerialFromDevice(string deviceId)
        {
            return deviceId.Split('&').Reverse().Skip(1).First().Split('\\').Last();
        }

        private static bool ManagedObjectIsHardDisk(ManagementBaseObject managedObject)
        {
            return Null.Bind(managedObject, mbo =>
                   Null.Bind(managedObject["InterfaceType"] as string, interfaceType =>
                   interfaceType != "USB"
                   ? Null.Return(true)
                   : Null.Bind(managedObject["MediaType"] as string, mediaType =>
                     Null.Return(mediaType.ToLower().Contains("external"))))) ?? true;
        }

        public static string GetDriveLetter(string physicalDrive)
        {
            try {
                using (var devs = new ManagementClass(@"Win32_Diskdrive"))
                {
                    foreach (var managedObject in devs.GetInstances())
                    {
                        if (Null.Bind(managedObject["DeviceID"] as string, deviceId =>
                            Null.Return(deviceId == physicalDrive)) ?? false)
                        {
                            Func<ManagementBaseObject, string, IEnumerable<ManagementBaseObject>> getRelated = (mbo, rel) =>
                                Null.Bind(mbo as ManagementObject, mo =>
                                Null.Bind(mo.GetRelated(rel), moc =>
                                moc.Cast<ManagementBaseObject>())) ??
                                Enumerable.Empty<ManagementBaseObject>();

                            foreach (var managedPartition in getRelated(managedObject, "Win32_DiskPartition")) {
                                foreach (var logicalDisk in getRelated(managedPartition, "Win32_LogicalDisk")) {
                                    return logicalDisk["DeviceID"] as string ?? "";
                                }
                            }
                        }
                    }
                    return "";
                }
            } catch (ManagementException) {
                return "";
            } catch (IOException) {
                return "";
            }
        }
    }
}