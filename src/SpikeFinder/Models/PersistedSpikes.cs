using System.Collections.Generic;
using System.Linq;

namespace SpikeFinder.Models
{
    public record PersistedSpikes(int? PosteriorCornea, int? AnteriorLens, int? PosteriorLens, int? ILM, int? RPE, string Notes, MeasureMode? MeasureMode, int? Choroid, int? Aqueous_C1α, int? C1α_C1β, int? C1β_C2, int? C2_C3, int? C3_C4, int? C4_Nucleus, int? Nucleus_C4, int? C4_C3, int? C3_C2, int? C2_C1β, int? C1β_C1α, int? C1α_Vitreous)
    {
        protected static int? GetCursorPosition(IEnumerable<CursorPosition> cursors, CursorElement cursor) => cursors.FirstOrDefault(y => y.CursorElement == cursor)?.X;
        public PersistedSpikes(IEnumerable<CursorPosition> cursors, string notes, MeasureMode? measureMode)
            : this(GetCursorPosition(cursors, CursorElement.PosteriorCornea), GetCursorPosition(cursors, CursorElement.AnteriorLens), GetCursorPosition(cursors, CursorElement.PosteriorLens), GetCursorPosition(cursors, CursorElement.ILM), GetCursorPosition(cursors, CursorElement.RPE), notes, measureMode, GetCursorPosition(cursors, CursorElement.Choroid), GetCursorPosition(cursors, CursorElement.Aqueous_C1α), GetCursorPosition(cursors, CursorElement.C1α_C1β), GetCursorPosition(cursors, CursorElement.C1β_C2), GetCursorPosition(cursors, CursorElement.C2_C3), GetCursorPosition(cursors, CursorElement.C3_C4), GetCursorPosition(cursors, CursorElement.C4_Nucleus), GetCursorPosition(cursors, CursorElement.Nucleus_C4), GetCursorPosition(cursors, CursorElement.C4_C3), GetCursorPosition(cursors, CursorElement.C3_C2), GetCursorPosition(cursors, CursorElement.C2_C1β), GetCursorPosition(cursors, CursorElement.C1β_C1α), GetCursorPosition(cursors, CursorElement.C1α_Vitreous)) { }
    }
}
