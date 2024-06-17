using System.ComponentModel;

namespace SpikeFinder.RefractiveIndices
{
    public enum RefractiveIndexMethods
    {
        [Description("A&S Cauchy (High Lens)")] ASCauchyHighLens,
        [Description("A&S Cauchy (Low Lens)")] ASCauchyLowLens,
        [Description("A&S Gullstrand (555)")] ASGullstrand555,
        [Description("A&S Gullstrand (589)")] ASGullstrand589,
        [Description("A&S Le Grand (555)")] ASLeGrand555,
        [Description("A&S Le Grand (589)")] ASLeGrand589,
        Argos,
        [Description("Cornu Le Grand")] CornuLeGrand,
        [Description("D&M Gullstrand (555)")] DMGullstrand555,
        [Description("D&M Gullstrand (589)")] DMGullstrand589,
        [Description("D&M Le Grand (555)")] DMLeGrand555,
        [Description("D&M Le Grand (589)")] DMLeGrand589,
        Lenstar,
        [Description("Liou & Brennan")] LiouBrennan,
        Navarro,
        Air,
    }
}
