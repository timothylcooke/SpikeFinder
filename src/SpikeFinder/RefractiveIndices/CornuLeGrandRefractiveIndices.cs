using System;

namespace SpikeFinder.RefractiveIndices
{
    public sealed class CornuLeGrandRefractiveIndices : RefractiveIndexMethod
    {
        public static CornuLeGrandRefractiveIndices Instance { get; } = new();

        private CornuLeGrandRefractiveIndices() { }

        private double ComputeRefractiveIndex(double nInfinity, double k, double lambda0, double wavelength)
        {
            var np = nInfinity + k / (wavelength - lambda0);
            var dnpdlambda = -k / Math.Pow(wavelength - lambda0, 2);

            return np - wavelength * dnpdlambda;
        }


        protected override double ComputeCornea(double wavelength) => ComputeRefractiveIndex(1.3610, 7.4147, 130, wavelength);
        protected override double ComputeAqueous(double wavelength) => ComputeRefractiveIndex(1.3221, 7.0096, 130, wavelength);
        protected override double ComputeLens(double wavelength) => ComputeRefractiveIndex(1.3999, 9.2492, 130, wavelength);
        protected override double ComputeVitreous(double wavelength) => ComputeRefractiveIndex(1.3208, 6.9806, 130, wavelength);
    }
}
