using System.ComponentModel;

namespace SpikeFinder.RefractiveIndices
{
    public enum RefractiveIndexMethods
    {
        Argos,
        [Description("Cornu Le Grand")] CornuLeGrand,
        [Description("D&M Gullstrand (555)")] DMGullstrand555,
        [Description("D&M Gullstrand (589)")] DMGullstrand589,
        [Description("D&M Le Grand (555)")] DMLeGrand555,
        [Description("D&M Le Grand (589)")] DMLeGrand589,
        Lenstar,
        Navarro,
    }
}
