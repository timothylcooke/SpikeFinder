using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SpikeFinder.Extensions;
using SpikeFinder.Models;
using SpikeFinder.RefractiveIndices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
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


        public static string DefaultSqlitePath = SettingsFilePath(Environment.SpecialFolder.CommonApplicationData, "Database.sqlite3db");

        protected static T GetInstance<T>(Environment.SpecialFolder folder, ref T? instance)
            where T : SfSettings
        {
            lock (typeof(SfSettings))
            {
                if (instance is null)
                {
                    var filePath = SettingsFilePath(folder, "Settings.json");

                    var initialJson = File.Exists(filePath) ? File.ReadAllText(filePath) : "{}";

                    try
                    {
                        instance = JsonConvert.DeserializeObject<T>(initialJson);
                    }
                    catch
                    {
                        instance = null;
                    }

                    if (instance == null)
                    {
                        instance = JsonConvert.DeserializeObject<T>("{}");
                    }

                    JsonConverter[] converters = { new StringEnumConverter() };
                    var instanceRef = instance;

                    // If any properties are changed, save the settings.
                    Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(x => instanceRef!.PropertyChanged += x, x => instanceRef!.PropertyChanged -= x)
                        .Select(x => x.EventArgs)
                        .Throttle(TimeSpan.FromMilliseconds(500)).Select(_ => JsonConvert.SerializeObject(instanceRef, converters)).StartWith(initialJson).DistinctUntilChanged().Skip(1)
                        .SelectMany((x, _) => Observable.StartAsync(token => File.WriteAllTextAsync(filePath, x, token), RxApp.TaskpoolScheduler))
                        .CatchAndShowErrors().Subscribe().DisposeWith(instance!._disposables);
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
        [Reactive, DataMember] public RefractiveIndexMethods RefractiveIndexMethod { get; set; } = RefractiveIndexMethods.Lenstar;
        [Reactive, DataMember] public string PseudophakicDefaultRefractiveIndex { get; set; } = "1.48";
        [Reactive, DataMember] public string PseudophakicAcrylicRefractiveIndex { get; set; } = "1.505";
        [Reactive, DataMember] public string PseudophakicPMMARefractiveIndex { get; set; } = "1.49";
        [Reactive, DataMember] public string PseudophakicSiliconeRefractiveIndex { get; set; } = "1.44";
        [Reactive, DataMember] public string RetinaRefractiveIndex { get; set; } = "1.4";
        [Reactive, DataMember] public string SiliconeOilRefractiveIndex { get; set; } = "1.406";

        public double GetLensRefractiveIndex(MeasureMode measureMode, double wavelength, RefractiveIndexMethod? refractiveIndexMethod = null) => GetLensRefractiveIndex(RefractiveIndices.RefractiveIndexMethod.GetLensMaterial(measureMode), wavelength, refractiveIndexMethod);
        public double GetLensRefractiveIndex(LensMaterial material, double wavelength, RefractiveIndexMethod? refractiveIndexMethod = null) => ((Func<double, double>)(
            material switch
            {
                LensMaterial.PseudophakicDefault => CustomRefractiveIndex.FromEquation(PseudophakicDefaultRefractiveIndex).ComputeRefractiveIndex,
                LensMaterial.PseudophakicAcrylic => CustomRefractiveIndex.FromEquation(PseudophakicAcrylicRefractiveIndex).ComputeRefractiveIndex,
                LensMaterial.PseudophakicPMMA => CustomRefractiveIndex.FromEquation(PseudophakicPMMARefractiveIndex).ComputeRefractiveIndex,
                LensMaterial.PseudophakicSilicone => CustomRefractiveIndex.FromEquation(PseudophakicSiliconeRefractiveIndex).ComputeRefractiveIndex,
                _ => (refractiveIndexMethod ?? RefractiveIndices.RefractiveIndexMethod.Current).Lens
            }))(wavelength);
        public double GetVitreousRefractiveIndex(MeasureMode measureMode, double wavelength, RefractiveIndexMethod? refractiveIndexMethod = null) => GetVitreousRefractiveIndex(RefractiveIndices.RefractiveIndexMethod.GetVitreousMaterial(measureMode), wavelength, refractiveIndexMethod);
        public double GetVitreousRefractiveIndex(VitreousMaterial material, double wavelength, RefractiveIndexMethod? refractiveIndexMethod = null) => ((Func<double, double>)(
            material switch
            {
                VitreousMaterial.SiliconeOil => CustomRefractiveIndex.FromEquation(SiliconeOilRefractiveIndex).ComputeRefractiveIndex,
                _ => (refractiveIndexMethod ?? RefractiveIndices.RefractiveIndexMethod.Current).Vitreous
            }))(wavelength);
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
