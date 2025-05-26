using System.ComponentModel;

namespace SpikeFinder.Models
{
    public enum CursorElement : byte
    {
        AnteriorCornea = 0,

        [Description("Posterior Cornea")]
        PosteriorCornea = 1,

        [Description("Anterior Lens")]
        AnteriorLens = 4,

        [Description("Posterior Lens")]
        PosteriorLens = 5,

        [Description("ILM")]
        ILM = 6,

        [Description("RPE")]
        RPE = 7,

        [Description("Choroid")]
        Choroid = 8,
    }
}
