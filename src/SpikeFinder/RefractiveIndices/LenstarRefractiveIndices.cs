using System;

namespace SpikeFinder.RefractiveIndices
{
    public class LenstarRefractiveIndices : RefractiveIndexMethod
    {
        public static LenstarRefractiveIndices Instance { get; } = new();

        private LenstarRefractiveIndices() { }

        private double ComputeRefractiveIndex(double nInfinity, double k, double lambda0, double wavelength)
        {
            var np = nInfinity + k / (wavelength - lambda0);

            return np / (1 - k / np * wavelength / Math.Pow(wavelength - lambda0, 2));
        }

        protected override double ComputeCornea(double wavelength) => ComputeRefractiveIndex(1.3217, 7.4147, 130, wavelength);
        protected override double ComputeAqueous(double wavelength) => ComputeRefractiveIndex(1.3221, 7.0096, 130, wavelength); 
        protected override double ComputeLens(double wavelength) => ComputeRefractiveIndex(1.3899, 9.2492, 130, wavelength);
        protected override double ComputeVitreous(double wavelength) => ComputeRefractiveIndex(1.3208, 6.9806, 130, wavelength);
        protected override double ComputeRetina(double wavelength) => 1.4;
    }
}
