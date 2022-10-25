using System;
using System.Linq;

namespace SpikeFinder.RefractiveIndices
{
    public sealed class NavarroRefractiveIndices : RefractiveIndexMethod
    {
        public static NavarroRefractiveIndices Instance { get; } = new();

        private NavarroRefractiveIndices() { }

        private readonly double[] A0 = {  0.66147196, -4.20146383,  6.29834237, -1.75835059 };
        private readonly double[] A1 = { -0.40352796,  2.73508956, -4.69409935,  2.36253794 };
        private readonly double[] P  = { -0.28046790,  1.50543784, -1.57508650,  0.35011657 };
        private readonly double[] R  = {  0.03385979, -0.11593235,  0.10293038, -0.02085782 };

        private const double lambda0 = 167.3;
        private const double lambda0Squared = lambda0 * lambda0;

        private double ComputeRefractiveIndex(double nStarStar, double nF, double nC, double nStar, double wavelength)
        {
            var wavelengthSquared = wavelength * wavelength;

            var a = Enumerable.Range(0, 4).Select(i => A0[i] + A1[i] * wavelengthSquared / 1E6 + P[i] * 1E6 / (wavelengthSquared - lambda0Squared) + R[i] * 1E12 / Math.Pow(wavelengthSquared - lambda0Squared, 2)).ToArray();
            var dadlambda = Enumerable.Range(0, 4).Select(i => 2 * A1[i] * wavelength / 1E6 - 2 * P[i] * wavelength * 1E6 / Math.Pow(wavelengthSquared - lambda0Squared, 2) - 4 * R[i] * wavelength * 1E12 / Math.Pow(wavelengthSquared - lambda0Squared, 3)).ToArray();

            var np = a[0] * nStarStar + a[1] * nF + a[2] * nC + a[3] * nStar;
            var dnpdlambda = dadlambda[0] * nStarStar + dadlambda[1] * nF + dadlambda[2] * nC + dadlambda[3] * nStar;

            return np - wavelength * dnpdlambda;
        }


        protected override double ComputeCornea(double wavelength) => ComputeRefractiveIndex(1.3975, 1.3807, 1.37405, 1.3668, wavelength);
        protected override double ComputeAqueous(double wavelength) => ComputeRefractiveIndex(1.3593, 1.3422, 1.3354, 1.3278, wavelength);
        protected override double ComputeLens(double wavelength) => ComputeRefractiveIndex(1.4492, 1.42625, 1.4175, 1.4097, wavelength);
        protected override double ComputeVitreous(double wavelength) => ComputeRefractiveIndex(1.3565, 1.3407, 1.3341, 1.3273, wavelength);
    }
}
