namespace SpikeFinder.RefractiveIndices
{
    public sealed class AirRefractiveIndices : RefractiveIndexMethod
    {
        public static AirRefractiveIndices Instance { get; } = new();

        private AirRefractiveIndices() { }

        protected override double ComputeCornea(double wavelength) => 1;
        protected override double ComputeAqueous(double wavelength) => 1;
        protected override double ComputeLens(double wavelength) => 1;
        protected override double ComputeVitreous(double wavelength) => 1;
    }
}
