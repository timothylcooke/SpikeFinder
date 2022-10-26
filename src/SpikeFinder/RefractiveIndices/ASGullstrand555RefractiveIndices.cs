namespace SpikeFinder.RefractiveIndices
{
    public sealed class ASGullstrand555RefractiveIndices : ASScaledRefractiveIndices
    {
        public static ASGullstrand555RefractiveIndices Instance { get; } = new();

        private ASGullstrand555RefractiveIndices() : base(555, (g, l) => g) { }
    }
}
