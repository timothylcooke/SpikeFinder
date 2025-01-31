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

            ToggleDropDownCommand.Do(x => x.IsDropDownOpen = !x.IsDropDownOpen).Subscribe();
        }

        [Reactive]
        public SfViewModel? CurrentViewModel { get; private set; }

        [Reactive]
        public string? Title { get; private set; }

        public ReactiveCommand<SplitButton, SplitButton> ToggleDropDownCommand { get; } = ReactiveCommand.Create<SplitButton, SplitButton>(x => x);

        public SourceList<ChildWindow> OpenDialogs { get; } = new();


        public string UrlPathSegment => "/MainWindow";
        public IScreen HostScreen => App.Bootstrapper;

    }
}
