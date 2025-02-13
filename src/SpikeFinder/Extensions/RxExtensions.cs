using System;
using System.Reactive.Linq;

namespace SpikeFinder.Extensions
{
    public static class RxExtensions
    {
        public static IObservable<T> CatchAndShowErrors<T>(this IObservable<T> input, bool restartOnThrow)
        {
            return input.Catch((Exception ex) =>
            {
                App.SpikeFinderMainWindow.NotifyException(ex);

                return restartOnThrow ? input.CatchAndShowErrors(restartOnThrow) : Observable.Empty<T>();
            });
        }
        public static IObservable<bool> Invert(this IObservable<bool> input) => input.Select(x => !x);
    }
}
