namespace SpikeFinder.RefractiveIndices
{
    public class ArgosRefractiveIndices : RefractiveIndexMethod
    {
        public static ArgosRefractiveIndices Instance { get; } = new();

        private ArgosRefractiveIndices() { }

        protected override double ComputeCornea(double wavelength) => 1.375;
        protected override double ComputeAqueous(double wavelength) => 1.336;
        protected override double ComputeLens(double wavelength) => 1.410;
        protected override double ComputeVitreous(double wavelength) => 1.336;
        protected override double ComputeRetina(double wavelength) => 1.4;
    }
}
