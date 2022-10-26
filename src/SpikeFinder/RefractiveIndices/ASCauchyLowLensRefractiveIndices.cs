namespace SpikeFinder.RefractiveIndices
{
    public sealed class ASCauchyLowLensRefractiveIndices : ASRefractiveIndices
    {
        public static ASCauchyLowLensRefractiveIndices Instance { get; } = new();

        private ASCauchyLowLensRefractiveIndices() { }

        protected override double ComputeLens(double wavelength) => ComputeRefractiveIndex(aToDLowLens, wavelength, double.NaN, double.NaN);
    }
}
