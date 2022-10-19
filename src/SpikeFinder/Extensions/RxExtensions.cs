using System;
using System.Reactive.Linq;

namespace SpikeFinder.Extensions
{
    public static class RxExtensions
    {
        public static IObservable<T> CatchAndShowErrors<T>(this IObservable<T> input)
        {
            return input.Catch((Exception ex) =>
            {
                App.SpikeFinderMainWindow.NotifyException(ex);
                return Observable.Never<T>();
            });
        }
    }
}
