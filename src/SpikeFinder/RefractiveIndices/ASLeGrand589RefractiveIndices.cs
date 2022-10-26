namespace SpikeFinder.RefractiveIndices
{
    public sealed class ASLeGrand589RefractiveIndices : ASScaledRefractiveIndices
    {
        public static ASLeGrand589RefractiveIndices Instance { get; } = new();
        private ASLeGrand589RefractiveIndices() : base(589, (g, l) => l) { }
    }
}
