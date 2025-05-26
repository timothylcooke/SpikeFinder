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
    }
}
