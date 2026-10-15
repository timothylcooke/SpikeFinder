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

        [Description("Aqueous–C1α")]
        Aqueous_C1α = 9,
        [Description("C1α–C1β")]
        C1α_C1β = 10,
        [Description("C1β–C2")]
        C1β_C2 = 11,
        [Description("C2–C3")]
        C2_C3 = 12,
        [Description("C3–C4")]
        C3_C4 = 13,
        [Description("C4–Nucleus")]
        C4_Nucleus = 14,
        [Description("Nucleus–C4")]
        Nucleus_C4 = 15,
        [Description("C4–C3")]
        C4_C3 = 16,
        [Description("C3–C2")]
        C3_C2 = 17,
        [Description("C2–C1β")]
        C2_C1β = 18,
        [Description("C1β–C1α")]
        C1β_C1α = 19,
        [Description("C1α–Vitreous")]
        C1α_Vitreous = 20
    }
}
