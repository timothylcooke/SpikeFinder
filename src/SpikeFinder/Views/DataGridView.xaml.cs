using ReactiveUI;
using SpikeFinder.Extensions;
using SpikeFinder.Models;
using SpikeFinder.Settings;
using SpikeFinder.ViewModels;
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
            Grid.FixCopyPaste();

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
                    .Where(x => Keyboard.Modifiers == ModifierKeys.None && x.Key == Key.Up && Grid.SelectedItems.Count == 1 && Grid.SelectedItems[0] == Grid.GetRecordAtRowIndex(Grid.GetFirstRowIndex()))
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
            });
        }
    }
}
