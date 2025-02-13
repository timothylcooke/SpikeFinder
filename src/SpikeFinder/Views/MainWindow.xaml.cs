using DynamicData;
using MahApps.Metro.Controls;
using MahApps.Metro.SimpleChildWindow;
using ReactiveUI;
using SpikeFinder.Extensions;
using SpikeFinder.Settings;
using SpikeFinder.Toast;
using SpikeFinder.ViewModels;
using Splat;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ToastNotifications;
using ToastNotifications.Core;
using ToastNotifications.Lifetime;
using ToastNotifications.Lifetime.Clear;
using ToastNotifications.Position;

namespace SpikeFinder.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow, IViewFor<MainWindowViewModel>
    {
        public MainWindow()
        {
            InitializeComponent();

            ViewModel = Locator.Current.GetService<MainWindowViewModel>()!;

            _notifier = new Notifier(cfg =>
            {
                cfg.PositionProvider = new WindowPositionProvider(this, Corner.TopLeft, 20, 40);
                cfg.LifetimeSupervisor = new CountBasedLifetimeSupervisor(MaximumNotificationCount.FromCount(5));
                cfg.Dispatcher = Application.Current.Dispatcher;
                cfg.DisplayOptions.TopMost = false;
            });

            IDisposable? whenActivated = null;
            whenActivated = this.WhenActivated(d =>
            {
                d(whenActivated!);

                d(Observable.FromEventPattern<EventHandler, EventArgs>(e => Closed += e, e => Closed -= e)
                    .Subscribe(_ => Application.Current.Shutdown()));

                var isValidSettings = SfMachineSettings.Instance is { } settings && settings.SqliteDatabasePath is not null && settings.ConnectionString is not null;

                d((isValidSettings ? Observable.Return<IRoutableViewModel>(new LoadGridViewModel()) :
                    MySqlExtensions.ReadConnectionStringFromEyeSuite()
                    .Select(x =>
                    {
                        if (x is null)
                            return (IRoutableViewModel)new SettingsViewModel();

                        SfMachineSettings.Instance.ConnectionString ??= new ProtectedString(x);
                        SfMachineSettings.Instance.SqliteDatabasePath ??= SfSettings.DefaultSqlitePath;

                        return new LoadGridViewModel();
                    }))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .InvokeCommand(ViewModel.HostScreen.Router.Navigate));
            });

            ShowChildWindowCommand = ReactiveCommand.CreateFromTask<ChildWindow>(ShowChildWindowAsync);
        }

        private readonly Notifier _notifier;
        private readonly object _updateNotificationTag = new object();

        public void ClearNotifications()
        {
            _notifier.ClearMessages(new ClearAll());
        }
        public void ClearNotificationsByTag(object tag)
        {
            _notifier.ClearMessages(new ClearByTag(tag));
        }

        public void NotifyException(Exception ex)
        {
            NotifyException(ex, false);
        }
        private void NotifyException(Exception ex, bool isUpdateFlag)
        {
            var button = new ToastButton(ReactiveCommand.CreateFromObservable(() => ShowChildWindow(new ErrorDetailsChildWindow(ex))), null, "View Details");

            if (isUpdateFlag)
            {
                ClearNotificationsByTag(_updateNotificationTag);
            }

            Notify(Severity.Error, ex.Message, actionButton: button, tag: isUpdateFlag ? _updateNotificationTag : null);
        }

        public ReactiveCommand<ChildWindow, Unit> ShowChildWindowCommand { get; }

        public async Task ShowChildWindowAsync(ChildWindow dialog)
        {
            await ShowChildWindowAsync<object>(dialog);
        }
        public async Task<TResult> ShowChildWindowAsync<TResult>(ChildWindow dialog)
        {
            return await ShowChildWindowAsync(dialog, () => ChildWindowManager.ShowChildWindowAsync<TResult>(this, dialog, ChildWindowManager.OverlayFillBehavior.FullWindow));
        }
        private async Task<TResult> ShowChildWindowAsync<TResult>(ChildWindow dialog, Func<Task<TResult>> showChildWindow)
        {
            var otherDialogs = ViewModel.OpenDialogs.Items.Where(x => x.IsEnabled).ToList();

            otherDialogs.ForEach(x => x.IsEnabled = false);

            ViewModel.OpenDialogs.Add(dialog);
            try
            {
                return await showChildWindow();
            }
            finally
            {
                ViewModel.OpenDialogs.Remove(dialog);
                otherDialogs.ForEach(x => x.IsEnabled = true);
            }
        }
        public IObservable<Unit> ShowChildWindow(ChildWindow dialog)
        {
            return Observable.StartAsync(() => ShowChildWindowAsync(dialog));
        }
        public IObservable<TResult> ShowChildWindow<TResult>(ChildWindow dialog)
        {
            return Observable.StartAsync(() => ShowChildWindowAsync<TResult>(dialog));
        }

        public SfNotificationMessage ShowNotification(Severity notificationType, string message, string? title, ToastButton? actionButton, MessageOptions options)
        {
            var notification = new SfNotificationMessage(notificationType, message, title, actionButton, options);
            _notifier.Notify(() => notification);
            return notification;
        }
        public void Notify(Severity notificationType, string message, string? title = null, object? tag = null, ToastButton? actionButton = null, TimeSpan? dismissAfter = null)
        {
            NotifyMessage(() => ShowNotification(notificationType, message, title, actionButton, NewMessageOptions(tag)), dismissAfter);
        }
        private void NotifyMessage(Func<SfNotificationMessage> showNotification, TimeSpan? dismissAfter)
        {
            if (Dispatcher?.Thread == Thread.CurrentThread)
            {
                if (dismissAfter.HasValue)
                {
                    Observable.Start(showNotification, RxApp.MainThreadScheduler)
                        .Delay(dismissAfter.Value)
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(notification => notification.Close());
                }
                else
                {
                    Observable.Start(showNotification, RxApp.MainThreadScheduler).Subscribe();
                }
            }
            else
            {
                RxApp.MainThreadScheduler.Schedule(() => NotifyMessage(showNotification, dismissAfter));
            }
        }
        private MessageOptions NewMessageOptions(object? tag)
        {
            return new MessageOptions
            {
                CloseClickAction = _ => Focus(),
                Tag = tag ?? new object() // Temporary fix for https://github.com/rafallopatka/ToastNotifications/issues/101
            };
        }


        public MainWindowViewModel? ViewModel
        {
            get => (MainWindowViewModel)GetValue(DataContextProperty);
            set => SetValue(DataContextProperty, value);
        }
        object? IViewFor.ViewModel
        {
            get => ViewModel;
            set
            {
                if (value is MainWindowViewModel vm)
                {
                    ViewModel = vm;
                }
                else
                {
                    throw new Exception($"{nameof(ViewModel)} must be {nameof(MainWindowViewModel)}");
                }
            }
        }
    }
}
