#nullable enable
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace SpikeFinder.ViewModels
{
    public abstract class SfViewModel : ReactiveObject, IRoutableViewModel, IActivatableViewModel
    {
        protected SfViewModel()
        {
            NavigateBackCommand = HostScreen.Router.NavigateBack;
        }

        protected IDisposable CreateSettingsCommand()
        {
            return new CompositeDisposable()
            {
                { SettingsCommand = ReactiveCommand.Create(() => { }) },
                { SettingsCommand.Select(_ => new DatabaseSettingsViewModel() as IRoutableViewModel).InvokeCommand(HostScreen.Router.Navigate) }
            };
        }

        public abstract string? UrlPathSegment { get; }

        [Reactive] public ReactiveCommand<Unit, Unit>? SettingsCommand { get; protected set; }
        [Reactive] public ReactiveCommand<Unit, Unit>? SaveCommand { get; protected set; }
        [Reactive] public ReactiveCommand<Unit, IRoutableViewModel?> NavigateBackCommand { get; protected set; }

        public abstract string? Title { get; }
        public IScreen HostScreen => App.Bootstrapper;
        public ViewModelActivator Activator { get; } = new ViewModelActivator();
    }
}
