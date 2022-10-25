#nullable enable

using FuzzySharp;
using SpikeFinder.Extensions;
using SpikeFinder.RefractiveIndices;
using SpikeFinder.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using static SpikeFinder.Models.MeasureMode;

namespace SpikeFinder.Models
{
    public record LenstarExam(
        Guid Uuid,
        int ExamId,
        Eye Eye,
        string PatientNumber,
        string LastName,
        string FirstName,
        DateTime DOB,
        DateTime Timestamp,
        MeasureMode? LenstarMeasureMode,
        double? Wavelength,
        ValueWithStandardDeviation? CCT,
        ValueWithStandardDeviation? AD,
        ValueWithStandardDeviation? LT,
        ValueWithStandardDeviation? VD,
        ValueWithStandardDeviation? RT,
        ValueWithStandardDeviation? AL,
        ValueWithStandardDeviation? K1,
        ValueWithStandardDeviation? K2,
        ValueWithStandardDeviation? Axis1,
        ValueWithStandardDeviation? WTW,
        ValueWithStandardDeviation? ICX,
        ValueWithStandardDeviation? ICY,
        ValueWithStandardDeviation? PD,
        ValueWithStandardDeviation? PCX,
        ValueWithStandardDeviation? PCY,
        PersistedSpikes? PersistedSpikes) : INotifyPropertyChanged
    {
        public static string ComputeKey(Guid Uuid, Eye Eye) => $"{Uuid}:{Eye}";

        public string Key { get; } = ComputeKey(Uuid, Eye);
        public string SearchText { get; } = $"{FirstName} {LastName} {PatientNumber} {DOB:MM/dd/yyyy}".ToLowerInvariant();

        public MeasureMode? MeasureMode => PersistedSpikes?.MeasureMode ?? LenstarMeasureMode;
        public string? MeasureModeDescription => MeasureMode is { } m ? _measureModesWithDescription[m] : null;
        public bool IsMatch(string searchQuery) => string.IsNullOrWhiteSpace(searchQuery) || Fuzz.PartialRatio(searchQuery, SearchText) >= 95;

        public bool HasSpikes => PersistedSpikes != null;
        public double? SpikeFinderCCT
        {
            get
            {
                if (PersistedSpikes != null && Wavelength != null)
                {
                    return (PersistedSpikes.PosteriorCornea - 1000.0) / 1250.0 / RefractiveIndexMethod.Current.Cornea(Wavelength.Value);
                }

                return null;
            }
        }
        public double? SpikeFinderAD
        {
            get
            {
                if (PersistedSpikes != null && Wavelength != null)
                {
                    var ad = (PersistedSpikes.AnteriorLens - PersistedSpikes.PosteriorCornea) / 1250.0 / RefractiveIndexMethod.Current.Aqueous(Wavelength.Value);

                    return ad - 0.1;
                }

                return null;
            }
        }
        public double? SpikeFinderLT
        {
            get
            {
                if (PersistedSpikes != null && Wavelength != null && MeasureMode is { } mm)
                {
                    return (PersistedSpikes.PosteriorLens - PersistedSpikes.AnteriorLens) / 1250.0 / SfMachineSettings.Instance.GetLensRefractiveIndex(mm, Wavelength.Value);
                }

                return null;
            }
        }
        public double? SpikeFinderVD
        {
            get
            {
                if (PersistedSpikes != null && Wavelength != null && MeasureMode is { } mm)
                {
                    return (PersistedSpikes.ILM - PersistedSpikes.PosteriorLens) / 1250.0 / SfMachineSettings.Instance.GetVitreousRefractiveIndex(mm, Wavelength.Value);
                }

                return null;
            }
        }
        public double? SpikeFinderRT
        {
            get
            {
                if (PersistedSpikes != null && Wavelength != null && MeasureMode is { } mm)
                {
                    return (PersistedSpikes.RPE - PersistedSpikes.ILM) / 1250.0 / RefractiveIndexMethod.Current.RefractiveIndex(Dimension.RT, mm, Wavelength.Value);
                }

                return null;
            }
        }
        public double? SpikeFinderAL
        {
            get
            {
                if (PersistedSpikes != null && Wavelength != null && MeasureMode is { } mm)
                {
                    var al = (PersistedSpikes.RPE - 1000.0) / 1250.0 / RefractiveIndexMethod.Current.AxialLength(Wavelength.Value, mm);

                    return al - 0.2 + (al - 0.2 - 23.776) * 0.0552 +
                        (
                        // We will only modify the (non-Sum-of-Segments) "SpikeFinder AL" if the refractive indices are "Lenstar."
                        SfMachineSettings.Instance.RefractiveIndexMethod is RefractiveIndexMethods.Lenstar ?
                        mm switch
                        {
                            APHAKIC => 0.21,
                            PSEUDOPHAKIC_DEFAULT => 0.11,
                            PHAKIC_SIL_OIL => -0.74,
                            APHAKIC_SIL_OIL => -0.86,
                            PSEUDOPHAKIC_SIL_OIL => -0.75,
                            PHAKIC_IOL => -0.02,
                            PSEUDOPHAKIC_PMMA => 0.11,
                            PSEUDOPHAKIC_ACRYLIC => 0.10,
                            PSEUDOPHAKIC_SILICONE => 0.12,
                            PSEUDOPHAKIC_PMMA_SIL_OIL => -0.75,
                            PSEUDOPHAKIC_ACRYLIC_SIL_OIL => -0.76,
                            PSEUDOPHAKIC_SILICONE_SIL_OIL => -0.74,
                            _ => 0, // PHAKIC, LS900_CHECK_UNIT, SHORT_EYE_PHAKIC
                        }
                        : 0);
                }

                return null;
            }
        }

        private static readonly Dictionary<MeasureMode, string> _measureModesWithDescription = EnumExtensions.GetEnumDescriptions<MeasureMode>();
        public static Dictionary<MeasureMode, string> GetMeasureModesWithDescription() => _measureModesWithDescription.ToDictionary(x => x.Key, x => x.Value); // Clone the array so the caller can't mess with the original.

        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnRefractiveIndexMethodChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SpikeFinderCCT)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SpikeFinderAD)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SpikeFinderLT)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SpikeFinderVD)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SpikeFinderAL)));
        }
        public void OnLensRefractiveIndexChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SpikeFinderLT)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SpikeFinderAL)));
        }
        public void OnVitreousRefractiveIndexChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SpikeFinderVD)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SpikeFinderAL)));
        }
        public void OnRetinaRefractiveIndexChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SpikeFinderRT)));
        }
    }
}
