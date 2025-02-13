using ReactiveUI;
using ReactiveUI.SourceGenerators;
using SpikeFinder.Models;
using System;
using System.Windows.Media;

namespace SpikeFinder.ViewModels;

public partial class ChooseExportRangeViewModel : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();

    public ChooseExportRangeViewModel(Geometry[][] geometries)
    {
        Geometries = geometries;
    }

    public Geometry[][] Geometries { get; }

    [Reactive] private double? _startPosition = null;
    [Reactive] private double? _endPosition = null;

    [ReactiveCommand] private ExportRange ExportEntireSpike() => new(0, 1, null);

    private IObservable<bool> CanExportSelectedRegion() => this.WhenAnyValue(x => x.StartPosition, x => x.EndPosition, (x, y) => x.HasValue && y.HasValue && x != y);
    [ReactiveCommand(CanExecute = nameof(CanExportSelectedRegion))] private ExportRange ExportSelectedRegion() => new(Math.Min(StartPosition ?? 0, EndPosition ?? 1), Math.Max(StartPosition ?? 0, EndPosition ?? 1), null);
}
