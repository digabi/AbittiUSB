using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;
using WPFLocalizeExtension.Extensions;

namespace Ytl.AbittiUsb
{
    internal static class LocalizationManager
    {
        private static readonly List<Tuple<WeakReference, Action<object>>> Watches =
            new List<Tuple<WeakReference, Action<object>>>();

        /// <summary>Adds a new watch and cleans up watches that have become garbage.</summary>
        private static void Add(object targetObj, Action<object> actionObj) {
            lock (Watches) {
                var i = 0;
                while (i < Watches.Count) {
                    var w = Watches[i];
                    var t = Watches[i].Item1.Target;
                    if (null == t || targetObj == t) {
                        var last = Watches.Count - 1;
                        Watches[i] = Watches[last];
                        Watches.RemoveAt(last);
                    } else {
                        ++i;
                    }
                }
                Watches.Add(Tuple.Create(new WeakReference(targetObj), actionObj));
            }
        }

        public static void SetWatch<T>(string key, T target, Func<string>[] values, Action<T, string> set) where T: class {
            Action<object> actionObj = (targetObj) => {
                var strings = new string[values.Length];
                for (int i = 0; i < values.Length; ++i)
                    strings[i] = values[i]();
                set(targetObj as T, string.Format(GetLocalizedValue<string>(key), strings));
            };
            actionObj(target);
            Add(target, actionObj);
        }

        public static void SetWatch(string key, TextBlock targetObj, params Func<string> [] values) {
            SetWatch(key, targetObj, values, (target, text) => target.Text = text);
        }

        public static void SetWatch(string key, Button targetObj, params Func<string>[] values) {
            SetWatch(key, targetObj, values, (target, text) => target.Content = text);
        }

        public static void SetWatch(string key, Window targetObj, params Func<string>[] values) {
            SetWatch(key, targetObj, values, (target, text) => target.Title = text);
        }

        public static void SetWatch(string key, Run targetObj, params Func<string>[] values) {
            SetWatch(key, targetObj, values, (target, text) => target.Text = text);
        }

        /// <summary>Updates the watched fields and also drops watches that are no longer used.</summary>
        public static void UpdateFieldsAfterLanguageChange() {
            lock (Watches) {
                var i = 0;
                while (i < Watches.Count) {
                    var targetObj = Watches[i].Item1.Target;
                    if (null == targetObj) {
                        var last = Watches.Count - 1;
                        Watches[i] = Watches[last];
                        Watches.RemoveAt(last);
                    } else {
                        Watches[i].Item2(targetObj);
                        ++i;
                    }
                }
            }
        }

        public static T GetLocalizedValue<T>(string key)
        {
            return LocExtension.GetLocalizedValue<T>(Assembly.GetCallingAssembly().GetName().Name + ":Strings:" + key);
        }
    }
}