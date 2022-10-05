using ReactiveUI;
using System;

namespace SpikeFinder.Extensions
{
    public static class IActivatableViewExtensions
    {
        public static void WhenActivated(this IActivatableView @this)
        {
            IDisposable? whenActivated = null;

            whenActivated = @this.WhenActivated(d =>
            {
                d(whenActivated!);
            });
        }
    }
}
