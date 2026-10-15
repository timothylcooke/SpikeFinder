using SpikeFinder.Attributes;
using System.ComponentModel;

namespace SpikeFinder.Models
{
    public enum Dimension : byte
    {
        [Description("Central Corneal Thickness")]
        [DimensionCursors(CursorElement.AnteriorCornea, CursorElement.PosteriorCornea)]
        CCT = 0,
        [Description("Aqueous Depth")]
        [DimensionCursors(CursorElement.PosteriorCornea, CursorElement.AnteriorLens)]
        AD = 1,
        [Description("Lens Thickness")]
        [DimensionCursors(CursorElement.AnteriorLens, CursorElement.PosteriorLens)]
        LT = 5,
        [Description("Vitreous Depth")]
        [DimensionCursors(CursorElement.PosteriorLens, CursorElement.ILM)]
        VD = 6,
        [Description("Retina Thickness")]
        [DimensionCursors(CursorElement.ILM, CursorElement.RPE)]
        RT = 7,
        [Description("Axial Length")]
        AL = 8,
        [Description("Choroidal Thickness")]
        [DimensionCursors(CursorElement.RPE, CursorElement.Choroid)]
        ChT = 9,

        [Description("Anterior C1α")]
        [DimensionCursors(CursorElement.Aqueous_C1α, CursorElement.C1α_C1β)]
        AnteriorC1α = 10,
        [Description("Anterior C1β")]
        [DimensionCursors(CursorElement.C1α_C1β, CursorElement.C1β_C2)]
        AnteriorC1β = 11,
        [Description("Anterior C2")]
        [DimensionCursors(CursorElement.C1β_C2, CursorElement.C2_C3)]
        AnteriorC2 = 12,
        [Description("Anterior C3")]
        [DimensionCursors(CursorElement.C2_C3, CursorElement.C3_C4)]
        AnteriorC3 = 13,
        [Description("Anterior C4")]
        [DimensionCursors(CursorElement.C3_C4, CursorElement.C4_Nucleus)]
        AnteriorC4 = 14,
        [Description("Nucleus")]
        [DimensionCursors(CursorElement.C4_Nucleus, CursorElement.Nucleus_C4)]
        Nucleus = 15,
        [Description("Posterior C4")]
        [DimensionCursors(CursorElement.Nucleus_C4, CursorElement.C4_C3)]
        PosteriorC4 = 16,
        [Description("Posterior C3")]
        [DimensionCursors(CursorElement.C4_C3, CursorElement.C3_C2)]
        PosteriorC3 = 17,
        [Description("Posterior C2")]
        [DimensionCursors(CursorElement.C3_C2, CursorElement.C2_C1β)]
        PosteriorC2 = 18,
        [Description("Posterior C1β")]
        [DimensionCursors(CursorElement.C2_C1β, CursorElement.C1β_C1α)]
        PosteriorC1β = 19,
        [Description("Posterior C1α")]
        [DimensionCursors(CursorElement.C1β_C1α, CursorElement.C1α_Vitreous)]
        PosteriorC1α = 20,
    }
}
