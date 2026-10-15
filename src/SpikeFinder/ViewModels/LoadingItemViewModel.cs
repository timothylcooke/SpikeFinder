using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace SpikeFinder.ViewModels;

public partial class LoadingItemViewModel : ReactiveObject
{
    public LoadingItemViewModel(int step, string title)
    {
        Step = step;
        Title = title;
    }
    public IDisposable Initialize(IObservable<long?> totalExams)
    {
        return new CompositeDisposable(
                totalExams.BindTo(this, x => x.MaxPossibleProgress),
                this.WhenAnyValue(x => x.MaxPossibleProgress, x => x.SlowlyUpdatingProgress, (max, current) => (max, current)).Select(x => x.max.HasValue && x.current >= x.max.Value).BindTo(this, x => x.IsFinished),
                this.WhenAnyValue(x => x.MaxPossibleProgress, x => x.SlowlyUpdatingProgress, (max, current) => (max, current)).Select(x => !x.max.HasValue || (x.current == 0 && x.max > 0)).BindTo(this, x => x.IsIndeterminate)
            );
    }
    public int Step { get; }
    [Reactive] private string _title;
    [Reactive(SetModifier = AccessModifier.Private)] private bool _isIndeterminate;
    [Reactive(SetModifier = AccessModifier.Private)] private bool _isFinished;
    [Reactive(SetModifier = AccessModifier.Private)] private long? _maxPossibleProgress;
    [Reactive] private long _slowlyUpdatingProgress;
    public long ActualProgress { get; set; } = 0;
}
