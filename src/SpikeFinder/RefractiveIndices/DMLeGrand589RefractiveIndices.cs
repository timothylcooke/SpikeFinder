namespace SpikeFinder.RefractiveIndices
{
    public sealed class DMLeGrand589RefractiveIndices : DMLeGrandRefractiveIndices
    {
        public static DMLeGrand589RefractiveIndices Instance { get; } = new();

        private DMLeGrand589RefractiveIndices() : base(589) { }
    }
}
