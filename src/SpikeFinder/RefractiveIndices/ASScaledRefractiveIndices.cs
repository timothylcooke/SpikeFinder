using System;

namespace SpikeFinder.RefractiveIndices
{
    public abstract class ASScaledRefractiveIndices : ASRefractiveIndices
    {
        private double _nLambdaBar;
        private Func<double, double, double> _n555;
        protected ASScaledRefractiveIndices(double nLambdaBar, Func<double, double, double> n555)
        {
            _nLambdaBar = nLambdaBar;
            _n555 = n555;
        }
        protected override sealed double ComputeRefractiveIndexScaleFactor(double[] aToD, double wavelength, double n555G, double n555L, double nLambdaG, double dnLambdaGdlambda)
        {
            return _n555(n555G, n555L) / ComputeNp(aToD, _nLambdaBar);
        }
    }
}
