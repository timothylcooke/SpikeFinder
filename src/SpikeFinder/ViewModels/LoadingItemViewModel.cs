using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace SpikeFinder.ViewModels
{
    public class LoadingItemViewModel : ReactiveObject
    {
        public LoadingItemViewModel(int step, string title)
        {
            Step = step;
            Title = title;
        }
        public IDisposable Initialize(IObservable<long?> totalExams)
        {
            return new CompositeDisposable(
                    totalExams.ToPropertyEx(this, x => x.MaxPossibleProgress),
                    this.WhenAnyValue(x => x.MaxPossibleProgress, x => x.SlowlyUpdatingProgress, (max, current) => (max, current)).Select(x => x.max.HasValue && x.current >= x.max.Value).ToPropertyEx(this, x => x.IsFinished),
                    this.WhenAnyValue(x => x.MaxPossibleProgress, x => x.SlowlyUpdatingProgress, (max, current) => (max, current)).Select(x => !x.max.HasValue || (x.current == 0 && x.max > 0)).ToPropertyEx(this, x => x.IsIndeterminate)
                );
        }
        public int Step { get; }
        [Reactive] public string Title { get; set; }
        [ObservableAsProperty] public bool IsIndeterminate { get; }
        [ObservableAsProperty] public bool IsFinished { get; }
        [ObservableAsProperty] public long? MaxPossibleProgress { get; }
        [Reactive] public long SlowlyUpdatingProgress { get; set; }
        public long ActualProgress { get; set; } = 0;
    }
}
