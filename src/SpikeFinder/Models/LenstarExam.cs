#nullable enable

using FuzzySharp;
using SpikeFinder.RefractiveIndices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

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
        PersistedSpikes? PersistedSpikes)
    {
        public static string ComputeKey(Guid Uuid, Eye Eye) => $"{Uuid}:{Eye}";

        public string Key { get; } = ComputeKey(Uuid, Eye);
        public string SearchText { get; } = $"{FirstName} {LastName} {PatientNumber} {DOB:MM/dd/yyyy}".ToLowerInvariant();

        public MeasureMode? MeasureMode => PersistedSpikes?.MeasureMode ?? LenstarMeasureMode;
        public string? MeasureModeDescription => MeasureMode is { } m ? MeasureModesWithDescription[m] : null;
        public bool IsMatch(string searchQuery) => string.IsNullOrWhiteSpace(searchQuery) || Fuzz.PartialRatio(searchQuery, SearchText) >= 95;

        public bool HasSpikes => PersistedSpikes != null;
        public double? SpikeFinderCCT
        {
            get
            {
                if (PersistedSpikes != null && Wavelength != null)
                {
                    return (PersistedSpikes.PosteriorCornea - 1000.0) / 1250.0 / LenstarRefractiveIndices.Instance.Cornea(Wavelength.Value);
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
                    var ad = (PersistedSpikes.AnteriorLens - PersistedSpikes.PosteriorCornea) / 1250.0 / LenstarRefractiveIndices.Instance.Aqueous(Wavelength.Value);

                    return ad - 0.1;
                }

                return null;
            }
        }
        public double? SpikeFinderLT
        {
            get
            {
                if (PersistedSpikes != null && Wavelength != null)
                {
                    return (PersistedSpikes.PosteriorLens - PersistedSpikes.AnteriorLens) / 1250.0 / LenstarRefractiveIndices.Instance.Lens(Wavelength.Value);
                }

                return null;
            }
        }
        public double? SpikeFinderVD
        {
            get
            {
                if (PersistedSpikes != null && Wavelength != null)
                {
                    return (PersistedSpikes.ILM - PersistedSpikes.PosteriorLens) / 1250.0 / LenstarRefractiveIndices.Instance.Vitreous(Wavelength.Value);
                }

                return null;
            }
        }
        public double? SpikeFinderRT
        {
            get
            {
                if (PersistedSpikes != null && Wavelength != null)
                {
                    return (PersistedSpikes.RPE - PersistedSpikes.ILM) / 1250.0 / LenstarRefractiveIndices.Instance.Retina(Wavelength.Value);
                }

                return null;
            }
        }
        public double? SpikeFinderAL
        {
            get
            {
                if (PersistedSpikes != null && Wavelength != null)
                {
                    var al = (PersistedSpikes.RPE - 1000.0) / 1250.0 / LenstarRefractiveIndices.Instance.AxialLength(Wavelength.Value);

                    return al - 0.2 + (al - 0.2 - 23.776) * 0.0552;
                }

                return null;
            }
        }

        internal static readonly Dictionary<MeasureMode, string> MeasureModesWithDescription = typeof(MeasureMode).GetFields(BindingFlags.Public | BindingFlags.Static).ToDictionary(x => (MeasureMode)x.GetValue(null)!, x => x.GetCustomAttributes(typeof(DescriptionAttribute), false).OfType<DescriptionAttribute>().Single().Description);
    }
}
