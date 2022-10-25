#nullable enable
using ReactiveUI;
using SpikeFinder.Extensions;
using System;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SpikeFinder.Views
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView
    {
        public SettingsView()
        {
            InitializeComponent();

            IDisposable? whenActivated = null;

            whenActivated = this.WhenActivated(d =>
            {
                d(this.WhenAnyValue(x => x.ViewModel!.CurrentPage).WhereNotNull().Select(x => FindName($"{x}Settings") as DependencyObject).WhereNotNull()
                    .Select(FocusManager.GetFocusedElement).WhereNotNull()
                    .ObserveOnDispatcher(DispatcherPriority.Input).Subscribe(x => x.Focus()));

                d(whenActivated!);
            });
        }
    }
}
