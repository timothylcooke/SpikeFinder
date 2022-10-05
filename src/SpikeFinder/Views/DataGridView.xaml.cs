#nullable enable
using ReactiveUI;
using SpikeFinder.Extensions;
using SpikeFinder.Models;
using SpikeFinder.Settings;
using SpikeFinder.ViewModels;
using Syncfusion.UI.Xaml.Grid;
using Syncfusion.UI.Xaml.Grid.Helpers;
using System;
using System.Collections;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;

namespace SpikeFinder.Views
{
    /// <summary>
    /// Interaction logic for DataGridView.xaml
    /// </summary>
    public partial class DataGridView
    {
        public DataGridView()
        {
            InitializeComponent();

            this.WhenActivated(d =>
            {
                SearchBox.Focus();

                
                d(Observable.FromEventPattern<KeyEventHandler, KeyEventArgs>(x => SearchBox.PreviewKeyDown += x, x => SearchBox.PreviewKeyDown -= x)
                    .Select(x => x.EventArgs)
                    .Where(x => Keyboard.Modifiers == ModifierKeys.None && x.Key == Key.Down)
                    .Do(x => x.Handled = Grid.Focus())
                    .Where(_ => Grid.SelectedItems.Count == 0 && Grid.ItemsSource is IEnumerable items && items.OfType<object>().Any())
                    .Select(_ => Grid.GetVisualContainer().ScrollRows.GetVisibleLines())
                    .Where(x => x.FirstBodyVisibleIndex >= 0 && x.Count > x.FirstBodyVisibleIndex)
                    .Do(x => Grid.SelectedIndex = x[x.FirstBodyVisibleIndex].LineIndex - 1)
                    .Subscribe());

                var gridKeyDown = Grid.WhenPreviewKeyDown().Publish().RefCount();

                d(gridKeyDown
                    .Where(x => Keyboard.Modifiers == ModifierKeys.None && x.Key == Key.Up && Grid.SelectedIndex == 0)
                    .Do(x => x.Handled = true)
                    .Do(_ => SearchBox.Focus())
                    .Subscribe());

                // When the user presses return key or double-clicks a cell
                d(gridKeyDown
                    .Where(x => Keyboard.Modifiers == ModifierKeys.None && x.Key == Key.Enter && Grid.SelectedItems.Count == 1)
                    .Do(x => x.Handled = true)
                    .Select(_ => Unit.Default)
                    .Merge(
                        Grid.WhenCellDoubleTapped()
                            .Where(x => Keyboard.Modifiers == ModifierKeys.None && x.ChangedButton == MouseButton.Left && Grid.SelectedItems.Count == 1)
                            .Select(_ => Unit.Default)
                    )
                    .Select(_ => Grid.SelectedItem)
                    .OfType<LenstarExam>()
                    .WhereNotNull()
                    .Select(x => new LoadSpikesViewModel(x))
                    .Cast<IRoutableViewModel>()
                    .InvokeCommand(App.Bootstrapper.Router.Navigate));

                d(App.Bootstrapper.Router.Navigate.Select(_ => Grid.SelectedIndex).BindTo(ViewModel, x => x.SelectedRowIndex));

                d(Disposable.Create(() => SfUserSettings.Instance.ColumnOrder = Grid.Columns.Select(x => x.MappingName).ToList()));
                d(Disposable.Create(() => SfUserSettings.Instance.ColumnWidths = Grid.Columns.Select(x => x.ActualWidth).ToList()));
                if (SfUserSettings.Instance.ColumnOrder is { } order)
                {
                    if (!Grid.Columns.Select(x => x.MappingName).SequenceEqual(order))
                    {
                        for (int i = 0; i < order.Count; i++)
                        {
                            if (Grid.Columns[i].MappingName != order[i])
                            {
                                // Move the correct column to this index.

                                if (Grid.Columns[order[i]] is { } correctColumn)
                                {
                                    Grid.Columns.RemoveAt(Grid.Columns.IndexOf(correctColumn));
                                    Grid.Columns.Insert(i, correctColumn);
                                }
                            }
                        }
                    }
                }

                if (SfUserSettings.Instance.ColumnWidths is { } widths)
                {
                    for (int i = 0; i < widths.Count; i++)
                    {
                        Grid.Columns[i].Width = widths[i];
                    }
                }

                if (ViewModel?.SelectedRowIndex is not -1)
                {
                    // Wait for the exams to contain the selected item, then select it in the grid...
                    d(ViewModel.WhenAnyValue(x => x.Exams.Count)
                        .Where(x => x > 0)
                        .Take(1)
                        .Delay(TimeSpan.FromMilliseconds(10), RxApp.MainThreadScheduler)
                        .Select(_ => ViewModel!.SelectedRowIndex + (Grid.GetFirstDataRowIndex() == -1 ? 0 : Grid.GetFirstDataRowIndex()))
                        .Do(i => Grid.SelectRows(i, i))
                        .Subscribe());
                }

                // Persist vertical scroll offset to ViewModel
                if (Grid.GetVisualContainer() is { VScrollBar: { } scrollBar } container)
                {
                    if (ViewModel!.GridVScrollOffset > 0)
                    {
                        // As soon as we can scroll past zero, restore the VScrollOffset.
                        d(scrollBar.WhenAnyValue(x => x.Maximum, x => x.Value, (max, current) => (max, current))
                            .Where(x => x.max > x.current)
                            .Take(1)
                            .Delay(TimeSpan.FromMilliseconds(10), RxApp.MainThreadScheduler)
                            .Do(_ => container.SetVerticalOffset(ViewModel.GridVScrollOffset - scrollBar.Minimum))
                            .Subscribe());
                    }

                    // Any time we scroll, update ViewModel.GridVScrollOffset.
                    d(Observable
                        .FromEventPattern<EventHandler, EventArgs>(x => scrollBar.ValueChanged += x, x => scrollBar.ValueChanged -= x, RxApp.MainThreadScheduler)
                        .Select(_ => scrollBar.Value)
                        .StartWith(Math.Max(scrollBar.Value, ViewModel!.GridVScrollOffset))
                        .BindTo(ViewModel, x => x.GridVScrollOffset));
                }

                // Persist sorting
                if (ViewModel?.GridColumnSortInfo is { } sort)
                {
                    // We want to restore the sorting at least now, and again once there is at least one exam. If we sort when there are no exams, then the exams load, it doesn't actually sort the collection.
                    d(ViewModel.WhenAnyValue(x => x.Exams.Count)
                        .Where(x => x > 0)
                        .TakeUntil(x => x > 0)
                        .Do(_ => Grid.SortColumnDescriptions.Clear())
                        .Select(_ => new SortColumnDescription { ColumnName = sort.ColumnName, SortDirection = sort.SortDirection })
                        .Do(Grid.SortColumnDescriptions.Add)
                        .Subscribe());
                }

                d(Observable.FromEventPattern<GridSortColumnsChangedEventArgs>(x => Grid.SortColumnsChanged += x, x => Grid.SortColumnsChanged -= x)
                    .Select(x => x.EventArgs.AddedItems.Count == 0 ? null : new ColumnSortInfo(x.EventArgs.AddedItems[0].ColumnName, x.EventArgs.AddedItems[0].SortDirection))
                    .StartWith(ViewModel!.GridColumnSortInfo)
                    .BindTo(ViewModel, x => x.GridColumnSortInfo));
            });
        }
    }
}
