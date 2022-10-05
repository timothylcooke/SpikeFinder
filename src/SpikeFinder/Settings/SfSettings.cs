using Newtonsoft.Json;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace SpikeFinder.Settings
{
    public abstract class SfSettings : ReactiveObject, IDisposable
    {
        protected SfSettings()
        {
            _disposables = new();
        }

        protected CompositeDisposable _disposables;

        private static string AppDataFolder(Environment.SpecialFolder folder)
        {
            var dir = Path.Combine(Environment.GetFolderPath(folder), "SpikeFinder");

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            return dir;
        }
        internal static string SettingsFilePath(Environment.SpecialFolder folder, string fileName) => Path.Combine(AppDataFolder(folder), fileName);


        protected static T GetInstance<T>(Environment.SpecialFolder folder, ref T? instance)
            where T : SfSettings
        {
            lock (typeof(SfSettings))
            {
                if (instance is null)
                {
                    var filePath = SettingsFilePath(folder, "Settings.json");

                    var initialJson = File.Exists(filePath) ? File.ReadAllText(filePath) : "{}";

                    instance = JsonConvert.DeserializeObject<T>(initialJson);

                    var instanceRef = instance;

                    // If any properties are changed, save the settings.
                    Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(x => instanceRef!.PropertyChanged += x, x => instanceRef!.PropertyChanged -= x)
                        .Select(x => x.EventArgs)
                        .Throttle(TimeSpan.FromMilliseconds(500)).Select(_ => JsonConvert.SerializeObject(instanceRef)).StartWith(initialJson).DistinctUntilChanged().Skip(1)
                        .SelectMany((x, _) => Observable.StartAsync(token => File.WriteAllTextAsync(filePath, x, token), RxApp.TaskpoolScheduler))
                        .Catch((Exception ex) => Observable.Return(Unit.Default).Do(_ => App.SpikeFinderMainWindow.NotifyException(ex)).Skip(1)).Subscribe().DisposeWith(instance!._disposables);
                }
            }

            return instance;
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }

    [DataContract]
    public class SfUserSettings : SfSettings
    {
        public static SfUserSettings Instance => GetInstance(Environment.SpecialFolder.LocalApplicationData, ref _instance);
        private static SfUserSettings? _instance;


        [Reactive, DataMember] public List<string>? ColumnOrder { get; set; }
        [Reactive, DataMember] public List<double>? ColumnWidths { get; set; }
    }
    [DataContract]
    public class SfMachineSettings : SfSettings
    {
        public static SfMachineSettings Instance => GetInstance(Environment.SpecialFolder.CommonApplicationData, ref _instance);
        private static SfMachineSettings? _instance;

        [Reactive, DataMember] public string? SqliteDatabasePath { get; set; }
        [Reactive, DataMember] public ProtectedString? ConnectionString { get; set; }
    }
    public class ProtectedString
    {
        public ProtectedString() { }
        public ProtectedString(string value)
        {
            using var rng = RandomNumberGenerator.Create();

            rng.GetBytes(Entropy = new byte[32]);

            Value = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.LocalMachine);
        }

        public string Unprotect()
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Value!, Entropy, DataProtectionScope.LocalMachine));
        }

        public byte[]? Entropy { get; set; }
        public byte[]? Value { get; set; }
    }
}
