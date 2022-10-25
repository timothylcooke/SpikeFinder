namespace SpikeFinder.RefractiveIndices
{
    public sealed class DMGullstrand555RefractiveIndices : DMGullstrandRefractiveIndices
    {
        public static DMGullstrand555RefractiveIndices Instance { get; } = new();

        private DMGullstrand555RefractiveIndices() : base(555) { }
    }
}
