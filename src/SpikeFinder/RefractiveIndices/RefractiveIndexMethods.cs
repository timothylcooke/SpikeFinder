using System.ComponentModel;

namespace SpikeFinder.RefractiveIndices
{
    public enum RefractiveIndexMethods
    {
        Argos,
        [Description("Cornu Le Grand")] CornuLeGrand,
        Lenstar,
        Navarro,
    }
}
