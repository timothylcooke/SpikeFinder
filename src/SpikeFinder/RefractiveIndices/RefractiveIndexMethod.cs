using SpikeFinder.Models;
using Splat;
using System;

namespace SpikeFinder.RefractiveIndices
{
    public abstract class RefractiveIndexMethod
    {
        private MemoizingMRUCache<double, double> _cornea, _aqueous, _lens, _vitreous, _retina;
        protected RefractiveIndexMethod()
        {
            _cornea = new MemoizingMRUCache<double, double>((wavelength, _) => ComputeCornea(wavelength), 20);
            _aqueous = new MemoizingMRUCache<double, double>((wavelength, _) => ComputeAqueous(wavelength), 20);
            _lens = new MemoizingMRUCache<double, double>((wavelength, _) => ComputeLens(wavelength), 20);
            _vitreous = new MemoizingMRUCache<double, double>((wavelength, _) => ComputeVitreous(wavelength), 20);
            _retina = new MemoizingMRUCache<double, double>((wavelength, _) => ComputeRetina(wavelength), 20);
        }

        public double Cornea(double wavelength) => _cornea.Get(wavelength);
        public double Aqueous(double wavelength) => _aqueous.Get(wavelength);
        public double Lens(double wavelength) => _lens.Get(wavelength);
        public double Vitreous(double wavelength) => _vitreous.Get(wavelength);
        public double Retina(double wavelength) => _retina.Get(wavelength);
        public double AxialLength(double wavelength)
        {
            const double idealCornea = 0.00055;
            var idealAqueous = 0.00280;
            var idealLens = 0.00440;
            const double idealRetina = 0.0002;
            const double idealEyeLeng = 0.02331;

            var idealVitBody = idealEyeLeng - idealCornea - idealAqueous - idealLens;

            return (Cornea(wavelength) * idealCornea + Aqueous(wavelength) * idealAqueous + Lens(wavelength) * idealLens + Vitreous(wavelength) * idealVitBody + Retina(wavelength) * idealRetina) / (idealEyeLeng + idealRetina);
        }

        public double RefractiveIndex(Dimension dimension, double wavelength)
        {
            return dimension switch
            {
                Dimension.CCT => Cornea(wavelength),
                Dimension.AD => Aqueous(wavelength),
                Dimension.LT => Lens(wavelength),
                Dimension.VD => Vitreous(wavelength),
                Dimension.RT => Retina(wavelength),
                Dimension.AL => AxialLength(wavelength),
                _ => 0
            };
        }

        protected abstract double ComputeCornea(double wavelength);
        protected abstract double ComputeAqueous(double wavelength);
        protected abstract double ComputeLens(double wavelength);
        protected abstract double ComputeVitreous(double wavelength);
        protected abstract double ComputeRetina(double wavelength);
    }
}
