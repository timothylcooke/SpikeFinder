using SpikeFinder.Attributes;
using SpikeFinder.Extensions;
using SpikeFinder.Models;
using SpikeFinder.Settings;
using Splat;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using static SpikeFinder.Models.LensMaterial;

namespace SpikeFinder.RefractiveIndices
{
    public abstract class RefractiveIndexMethod
    {
        public static RefractiveIndexMethod Current => GetRefractiveIndexMethod(SfMachineSettings.Instance.RefractiveIndexMethod);
        public static RefractiveIndexMethod GetRefractiveIndexMethod(RefractiveIndexMethods method) => method switch
        {
            RefractiveIndexMethods.Lenstar => LenstarRefractiveIndices.Instance,
            RefractiveIndexMethods.Argos => ArgosRefractiveIndices.Instance,
            _ => throw new InvalidEnumArgumentException()
        };

        private static readonly Dictionary<MeasureMode, LensMaterial> _lensMaterials;
        private static readonly Dictionary<MeasureMode, VitreousMaterial> _vitreousMaterials;

        public static LensMaterial GetLensMaterial(MeasureMode measureMode) => _lensMaterials[measureMode];
        public static VitreousMaterial GetVitreousMaterial(MeasureMode measureMode) => _vitreousMaterials[measureMode];

        static RefractiveIndexMethod()
        {
            var materials = EnumExtensions.GetAllEnumValues<MeasureMode>().ToDictionary(x => x, x => x.GetCustomEnumAttributes<OcularMaterialAttribute>().Single());
            _lensMaterials = materials.ToDictionary(x => x.Key, x => x.Value.LensMaterial);
            _vitreousMaterials = materials.ToDictionary(x => x.Key, x => x.Value.VitreousMaterial);
        }


        private MemoizingMRUCache<double, double> _cornea, _aqueous, _lens, _vitreous, _retina, _axialLength;
        protected RefractiveIndexMethod()
        {
            _cornea = new MemoizingMRUCache<double, double>((wavelength, _) => ComputeCornea(wavelength), 20);
            _aqueous = new MemoizingMRUCache<double, double>((wavelength, _) => ComputeAqueous(wavelength), 20);
            _lens = new MemoizingMRUCache<double, double>((wavelength, _) => ComputeLens(wavelength), 20);
            _vitreous = new MemoizingMRUCache<double, double>((wavelength, _) => ComputeVitreous(wavelength), 20);
            _retina = new MemoizingMRUCache<double, double>((wavelength, _) => ComputeRetina(wavelength), 20);
            _axialLength = new MemoizingMRUCache<double, double>((wavelength, measureMode) => ComputeAxialLength(wavelength, (MeasureMode)measureMode!), 20);
        }

        public double Cornea(double wavelength) => _cornea.Get(wavelength);
        public double Aqueous(double wavelength) => _aqueous.Get(wavelength);
        public double Lens(double wavelength) => _lens.Get(wavelength);
        public double Vitreous(double wavelength) => _vitreous.Get(wavelength);
        public double Retina(double wavelength) => _retina.Get(wavelength);
        public double AxialLength(double wavelength, MeasureMode measureMode) => _axialLength.Get(wavelength, measureMode);

        public double RefractiveIndex(Dimension dimension, MeasureMode measureMode, double wavelength)
        {
            return dimension switch
            {
                Dimension.CCT => Cornea(wavelength),
                Dimension.AD => Aqueous(wavelength),
                Dimension.LT => SfMachineSettings.Instance.GetLensRefractiveIndex(_lensMaterials[measureMode], wavelength, this),
                Dimension.VD => SfMachineSettings.Instance.GetVitreousRefractiveIndex(_vitreousMaterials[measureMode], wavelength, this),
                Dimension.RT => CustomRefractiveIndex.FromEquation(SfMachineSettings.Instance.RetinaRefractiveIndex).ComputeRefractiveIndex(wavelength),
                Dimension.AL => AxialLength(wavelength, measureMode),
                _ => 0
            };
        }

        protected abstract double ComputeCornea(double wavelength);
        protected abstract double ComputeAqueous(double wavelength);
        protected abstract double ComputeLens(double wavelength);
        protected abstract double ComputeVitreous(double wavelength);
        protected abstract double ComputeRetina(double wavelength);
        public double ComputeAxialLength(double wavelength, MeasureMode measureMode)
        {
            var lensMaterial = _lensMaterials[measureMode];
            var vitreousMaterial = _vitreousMaterials[measureMode];

            const double idealCornea = 0.55E-3;
            var idealAqueous = lensMaterial switch 
            {
                Aphakic => 7.2E-3,
                PseudophakicAcrylic => 5.0E-3,
                PseudophakicDefault => 5.0E-3,
                PseudophakicPMMA => 5.0E-3,
                PseudophakicSilicone => 5.0E-3,
                Phakic => 2.8E-3,
                _ => throw new ArgumentOutOfRangeException()
            };

            var idealLens = lensMaterial switch
            {
                Aphakic => 0,
                PseudophakicAcrylic => 1E-3,
                PseudophakicDefault => 1E-3,
                PseudophakicPMMA => 1E-3,
                PseudophakicSilicone => 1E-3,
                Phakic => 4.4E-3,
                _ => throw new ArgumentOutOfRangeException()
            };

            const double idealRetina = 0.2E-3;
            const double idealEyeLeng = 23.31E-3;

            var idealVitBody = idealEyeLeng - idealCornea - idealAqueous - idealLens;

            return (Cornea(wavelength) * idealCornea
                 + Aqueous(wavelength) * idealAqueous
                 + SfMachineSettings.Instance.GetLensRefractiveIndex(lensMaterial, wavelength, this) * idealLens
                 + SfMachineSettings.Instance.GetVitreousRefractiveIndex(vitreousMaterial, wavelength, this) * idealVitBody
                 + Retina(wavelength) * idealRetina) / (idealEyeLeng + idealRetina);
        }
    }
}
