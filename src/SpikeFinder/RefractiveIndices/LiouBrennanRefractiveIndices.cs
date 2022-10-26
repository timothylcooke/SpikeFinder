namespace SpikeFinder.RefractiveIndices
{
    public sealed class LiouBrennanRefractiveIndices : RefractiveIndexMethod
    {
        public static LiouBrennanRefractiveIndices Instance { get; } = new();

        private LiouBrennanRefractiveIndices() { }

        private double ComputeRefractiveIndex(double n555, double wavelength)
        {
            var np = n555 + 0.0512 - 0.1455 * wavelength / 1E3 + 0.0961 * wavelength * wavelength / 1E6;
            var dnpdlambda = -0.1455 / 1E3 + 0.1922 * wavelength / 1E6;

            return np - wavelength * dnpdlambda;
        }

        protected override double ComputeCornea(double wavelength) => ComputeRefractiveIndex(1.376, wavelength);
        protected override double ComputeAqueous(double wavelength) => ComputeRefractiveIndex(1.336, wavelength); 
        protected override double ComputeLens(double wavelength) => ComputeRefractiveIndex(1.394, wavelength);
        protected override double ComputeVitreous(double wavelength) => ComputeRefractiveIndex(1.336, wavelength);
    }
}
