using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace SpikeFinder.Models
{
    public class CursorPosition : ReactiveObject
    {
        public CursorPosition BeginDrag()
        {
            _xBeforeDrag = X;
            return this;
        }
        public CursorPosition? CancelDrop()
        {
            X = _xBeforeDrag ?? X;
            return null;
        }

        private int? _xBeforeDrag;

        public CursorElement CursorElement { get; init; }
        public string? DisplayName { get; init; }
        [Reactive] public int? X { get; set; }
    }
}
