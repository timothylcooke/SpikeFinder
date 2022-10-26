namespace SpikeFinder.RefractiveIndices
{
    public sealed class ASLeGrand555RefractiveIndices : ASScaledRefractiveIndices
    {
        public static ASLeGrand555RefractiveIndices Instance { get; } = new();
        private ASLeGrand555RefractiveIndices() : base(555, (g, l) => l) { }
    }
}
