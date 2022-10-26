namespace SpikeFinder.RefractiveIndices
{
    public sealed class ASGullstrand589RefractiveIndices : ASScaledRefractiveIndices
    {
        public static ASGullstrand589RefractiveIndices Instance { get; } = new();

        private ASGullstrand589RefractiveIndices() : base(589, (g, l) => g) { }
    }
}
