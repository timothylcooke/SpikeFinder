namespace SpikeFinder.RefractiveIndices
{
    public sealed class DMGullstrand589RefractiveIndices : DMGullstrandRefractiveIndices
    {
        public static DMGullstrand589RefractiveIndices Instance { get; } = new();

        private DMGullstrand589RefractiveIndices() : base(589) { }
    }
}
