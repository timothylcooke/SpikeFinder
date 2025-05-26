using System.Collections.Generic;
using System.Linq;

namespace SpikeFinder.Models
{
    public record PersistedSpikes(int? PosteriorCornea, int? AnteriorLens, int? PosteriorLens, int? ILM, int? RPE, string Notes, MeasureMode? MeasureMode, int? Choroid)
    {
        protected static int? GetCursorPosition(IEnumerable<CursorPosition> cursors, CursorElement cursor) => cursors.FirstOrDefault(y => y.CursorElement == cursor)?.X;
        public PersistedSpikes(IEnumerable<CursorPosition> cursors, string notes, MeasureMode? measureMode)
            : this(GetCursorPosition(cursors, CursorElement.PosteriorCornea), GetCursorPosition(cursors, CursorElement.AnteriorLens), GetCursorPosition(cursors, CursorElement.PosteriorLens), GetCursorPosition(cursors, CursorElement.ILM), GetCursorPosition(cursors, CursorElement.RPE), notes, measureMode, GetCursorPosition(cursors, CursorElement.Choroid)) { }
    }
}
