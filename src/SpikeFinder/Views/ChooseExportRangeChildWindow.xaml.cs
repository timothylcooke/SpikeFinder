using MahApps.Metro.SimpleChildWindow;
using ReactiveMarbles.ObservableEvents;
using ReactiveUI;
using SpikeFinder.ViewModels;
using System;
using System.Reactive.Linq;
using System.Windows.Media;

namespace SpikeFinder.Views
{
    public partial class ChooseExportRangeChildWindow : ChildWindow, IViewFor<ChooseExportRangeViewModel>, IActivatableView
    {
        public ChooseExportRangeChildWindow()
        {
            InitializeComponent();

            IDisposable disposables = null!;

            disposables = this.WhenActivated(d =>
            {
                d(this.Events().PreviewMouseWheel.Subscribe(x => x.Handled = true));

                d(this.WhenAnyObservable(x => x.ViewModel!.ExportEntireSpikeCommand).Merge(this.WhenAnyObservable(x => x.ViewModel!.ExportSelectedRegionCommand)).Subscribe(x => Close(x)));

                d(disposables);
            });
        }
        public ChooseExportRangeChildWindow(Geometry[][] geometries)
            : this()
        {
            ViewModel = new ChooseExportRangeViewModel(geometries);
        }

        public ChooseExportRangeViewModel? ViewModel
        {
            get => (ChooseExportRangeViewModel?)DataContext;
            set => DataContext = value;
        }
        object? IViewFor.ViewModel
        {
            get => DataContext;
            set => DataContext = value;
        }
    }
}
