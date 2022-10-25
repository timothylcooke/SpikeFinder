namespace SpikeFinder.RefractiveIndices
{
    public sealed class DMLeGrand555RefractiveIndices : DMLeGrandRefractiveIndices
    {
        public static DMLeGrand555RefractiveIndices Instance { get; } = new();

        private DMLeGrand555RefractiveIndices() : base(555) { }
    }
}
