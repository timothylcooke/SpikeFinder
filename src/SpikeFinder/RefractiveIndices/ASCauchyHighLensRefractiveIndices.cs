namespace SpikeFinder.RefractiveIndices
{
    public sealed class ASCauchyHighLensRefractiveIndices : ASRefractiveIndices
    {
        public static ASCauchyHighLensRefractiveIndices Instance { get; } = new();

        private ASCauchyHighLensRefractiveIndices() { }
    }
}
