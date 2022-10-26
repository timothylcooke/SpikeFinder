using System;

namespace SpikeFinder.RefractiveIndices
{
    public abstract class ASRefractiveIndices : RefractiveIndexMethod
    {
        protected ASRefractiveIndices() { }

        protected double ComputeNp(double[] aToD, double wavelength)
        {
            return aToD[0] + aToD[1] / Math.Pow(wavelength, 2) + aToD[2] / Math.Pow(wavelength, 4) + aToD[3] / Math.Pow(wavelength, 6);
        }
        private double ComputeDnpdlambda(double[] aToD, double wavelength)
        {
            return -2 * aToD[1] / Math.Pow(wavelength, 3) - 4 * aToD[2] / Math.Pow(wavelength, 5) - 6 * aToD[3] / Math.Pow(wavelength, 7);
        }

        protected double ComputeRefractiveIndex(double[] aToD, double wavelength, double n555G, double n555L)
        {
            var nLambdaG = ComputeNp(aToD, wavelength);
            var dnLambdaGdlambda = ComputeDnpdlambda(aToD, wavelength);

            var scale = ComputeRefractiveIndexScaleFactor(aToD, wavelength, n555G, n555L, nLambdaG, dnLambdaGdlambda);

            return scale * (nLambdaG - wavelength * dnLambdaGdlambda);
        }

        protected virtual double ComputeRefractiveIndexScaleFactor(double[] aToD, double wavelength, double n555G, double n555L, double nLambdaG, double dnLambdaGdlambda) => 1;

        protected override sealed double ComputeCornea(double wavelength) => ComputeRefractiveIndex(aToDCornea, wavelength, 1.376, 1.3771);
        protected override sealed double ComputeAqueous(double wavelength) => ComputeRefractiveIndex(aToDAqueous, wavelength, 1.336, 1.3374);
        protected override double ComputeLens(double wavelength) => ComputeRefractiveIndex(aToDHighLens, wavelength, 1.3994, 1.42);
        protected override sealed double ComputeVitreous(double wavelength) => ComputeRefractiveIndex(aToDVitreous, wavelength, 1.336, 1.336);


        private static readonly double[] aToDCornea   = { 1.361594, 6009.687, -676076000, 59084500000000 };
        private static readonly double[] aToDAqueous  = { 1.321631, 6070.796, -706230500, 61478610000000 };
        private static readonly double[] aToDHighLens = { 1.389248, 6521.218, -611066100, 59081910000000 };
        protected static readonly double[] aToDLowLens  = { 1.369486, 6428.455, -602373800, 58241490000000 };
        private static readonly double[] aToDVitreous = { 1.322357, 5560.240, -581739100, 50368100000000 };
    }
}
