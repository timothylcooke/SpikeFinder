using System;

namespace SpikeFinder.RefractiveIndices
{
    public abstract class DMRefractiveIndices : RefractiveIndexMethod
    {
        private readonly double _lambdaBar;
        private readonly double[] A = { 0.5684027565, 0.1726177391, 0.02086189578, 0.1130748688 };
        private readonly double[] lambda2 = { 0.005101829712, 0.01821153936, 0.02620722293, 10.69792721 };
        private readonly double _nLambdaBarCornea, _nLambdaBarAqueous, _nLambdaBarLens, _nLambdaBarVitreous;

        protected DMRefractiveIndices(double lambdaBar, double nLambdaBarCornea, double nLambdaBarAqueous, double nLambdaBarLens, double nLambdaBarVitreous)
        {
            _lambdaBar = lambdaBar;
            _nLambdaBarCornea = nLambdaBarCornea;
            _nLambdaBarAqueous = nLambdaBarAqueous;
            _nLambdaBarLens = nLambdaBarLens;
            _nLambdaBarVitreous = nLambdaBarVitreous;
        }

        protected double ComputeRefractiveIndex(double nLambdaBar, double wavelength)
        {
            double n(double lambda)
            {
                var lambdaSquared = lambda * lambda;

                return Math.Sqrt(1 + A[0] * lambdaSquared / (lambdaSquared - 1E6 * lambda2[0]) + A[1] * lambdaSquared / (lambdaSquared - 1E6 * lambda2[1]) + A[2] * lambdaSquared / (lambdaSquared - 1E6 * lambda2[2]) + A[3] * lambdaSquared / (lambdaSquared - 1E6 * lambda2[3]));
            }

            var nLambdaBarW = n(_lambdaBar);
            var nLambdaW = n(wavelength);

            var lambdaSquared = wavelength * wavelength;
            var lambdaCubed = lambdaSquared * wavelength;
            var dnLambdaWdlambda = (A[0] * lambdaCubed / Math.Pow(lambdaSquared - 1E6 * lambda2[0], 2) - A[0] * wavelength / (lambdaSquared - 1E6 * lambda2[0]) + A[1] * lambdaCubed / Math.Pow(lambdaSquared - 1E6 * lambda2[1], 2) - A[1] * wavelength / (lambdaSquared - 1E6 * lambda2[1]) + A[2] * lambdaCubed / Math.Pow(lambdaSquared - 1E6 * lambda2[2], 2) - A[2] * wavelength / (lambdaSquared - 1E6 * lambda2[2]) + A[3] * lambdaCubed / Math.Pow(lambdaSquared - 1E6 * lambda2[3], 2) - A[3] * wavelength / (lambdaSquared - 1E6 * lambda2[3])) / Math.Sqrt(1 + A[0] * lambdaSquared / (lambdaSquared - 1E6 * lambda2[0]) + A[1] * lambdaSquared / (lambdaSquared - 1E6 * lambda2[1]) + A[2] * lambdaSquared / (lambdaSquared - 1E6 * lambda2[2]) + A[3] * lambdaSquared / (lambdaSquared - 1E6 * lambda2[3]));


            var np = nLambdaW * nLambdaBar / nLambdaBarW;
            var dnpdlambda = -nLambdaBar / nLambdaBarW * dnLambdaWdlambda;

            return np - wavelength * dnpdlambda;
        }

        protected override double ComputeCornea(double wavelength) => ComputeRefractiveIndex(_nLambdaBarCornea, wavelength);
        protected override double ComputeAqueous(double wavelength) => ComputeRefractiveIndex(_nLambdaBarAqueous, wavelength);
        protected override double ComputeLens(double wavelength) => ComputeRefractiveIndex(_nLambdaBarLens, wavelength);
        protected override double ComputeVitreous(double wavelength) => ComputeRefractiveIndex(_nLambdaBarVitreous, wavelength);
    }
}
