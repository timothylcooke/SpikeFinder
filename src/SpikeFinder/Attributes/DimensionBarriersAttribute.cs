using SpikeFinder.Models;
using System;

namespace SpikeFinder.Attributes
{
    class DimensionCursorsAttribute : Attribute
    {
        public CursorElement Start { get; }
        public CursorElement End { get; }

        public DimensionCursorsAttribute(CursorElement start, CursorElement end)
        {
            Start = start;
            End = end;
        }
    }
}
