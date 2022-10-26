using DynamicData;
using MahApps.Metro.Controls;
using MahApps.Metro.SimpleChildWindow;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Reactive.Linq;
using System.Windows.Threading;

namespace SpikeFinder.ViewModels
{
    public class MainWindowViewModel : ReactiveObject, IRoutableViewModel
    {
        public MainWindowViewModel()
        {
            var whenCurrentViewModel = HostScreen.Router.CurrentViewModel.WhereNotNull().OfType<SfViewModel>()
                .Publish()
                .RefCount();

            whenCurrentViewModel.Select(x => x.Title).Select(x => $"SpikeFinder{(x is null ? null : $" — {x}")}").BindTo(this, x => x.Title);
            whenCurrentViewModel.BindTo(this, x => x.CurrentViewModel);

            // Keep track of IsMeasureModeDropDownOpen; delay setting false until we actually render it so that if we click the button while it's open, this delayed observable still sees it as being open.
            var delayedIsMeasureModeDropDownOpen = false;
            
            var whenIsMeasureModeDropDownOpen = this.WhenAnyValue(x => x.IsMeasureModeDropDownOpen).Publish().RefCount();
            Observable.Merge(whenIsMeasureModeDropDownOpen.ObserveOnDispatcher(DispatcherPriority.Render), whenIsMeasureModeDropDownOpen.Where(x => x)).DistinctUntilChanged().Subscribe(x => delayedIsMeasureModeDropDownOpen = x);

            ToggleDropDownCommand.Select(button => (button, isDropDownOpen: delayedIsMeasureModeDropDownOpen)).ObserveOnDispatcher(DispatcherPriority.Input).Do(x => x.button.IsDropDownOpen = !x.isDropDownOpen).Subscribe();
        }

        [Reactive]
        public SfViewModel? CurrentViewModel { get; private set; }

        [Reactive]
        public string? Title { get; private set; }

        public ReactiveCommand<SplitButton, SplitButton> ToggleDropDownCommand { get; } = ReactiveCommand.Create<SplitButton, SplitButton>(x => x);
        [Reactive] public bool IsMeasureModeDropDownOpen { get; set; }

        public SourceList<ChildWindow> OpenDialogs { get; } = new();


        public string UrlPathSegment => "/MainWindow";
        public IScreen HostScreen => App.Bootstrapper;

    }
}
