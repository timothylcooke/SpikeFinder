using ReactiveUI;
using Syncfusion.Data.Extensions;
using Syncfusion.UI.Xaml.Grid;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace SpikeFinder.Extensions
{
    public static class SfDataGridExtensions
    {
        public static IObservable<KeyEventArgs> WhenPreviewKeyDown(this SfDataGrid grid)
        {
            return Observable.FromEventPattern<KeyEventHandler, KeyEventArgs>(
                    x => GridSelectionControllerEx.AddHandler(grid, x),
                    x => GridSelectionControllerEx.RemoveHandler(grid, x),
                    RxApp.MainThreadScheduler)
                .Select(x => x.EventArgs);
        }
        public static IObservable<GridCellDoubleTappedEventArgs> WhenCellDoubleTapped(this SfDataGrid grid)
        {
            return Observable.FromEventPattern<GridCellDoubleTappedEventArgs>(x => grid.CellDoubleTapped += x, x => grid.CellDoubleTapped -= x, RxApp.MainThreadScheduler)
                .Select(x => x.EventArgs);
        }

        private class GridSelectionControllerEx : GridSelectionController
        {
            private event KeyEventHandler? KeyDown;
            private GridSelectionControllerEx(SfDataGrid dataGrid) : base(dataGrid) { }

            public static void AddHandler(SfDataGrid grid, KeyEventHandler handler)
            {
                if (grid.SelectionController is not GridSelectionControllerEx)
                {
                    grid.SelectionController = new GridSelectionControllerEx(grid);
                }

                (grid.SelectionController as GridSelectionControllerEx)!.KeyDown += handler;
            }
            public static void RemoveHandler(SfDataGrid grid, KeyEventHandler handler)
            {
                if (grid.SelectionController is GridSelectionControllerEx controller)
                {
                    controller.KeyDown -= handler;
                }
                else
                {
                    throw new ArgumentException($"The grid does not currently have a {nameof(GridSelectionControllerEx)}.");
                }
            }
            public override bool HandleKeyDown(KeyEventArgs e)
            {
                KeyDown?.Invoke(DataGrid, e);
                return base.HandleKeyDown(e);
            }
        }

        public static void FixCopyPaste(this SfDataGrid grid)
        {
            if (grid.GridCopyPaste is not GridCutCopyPasteEx)
            {
                grid.GridCopyPaste = new GridCutCopyPasteEx(grid);
            }
        }

        private class GridCutCopyPasteEx : GridCutCopyPaste
        {
            public GridCutCopyPasteEx(SfDataGrid grid)
                : base(grid) { }
            protected override void CopyRows(ObservableCollection<object> records, ref StringBuilder text)
            {
                if (records == null || text == null)
                {
                    return;
                }

                if (dataGrid.GridCopyOption.HasFlag(GridCopyOption.IncludeHeaders))
                {
                    for (int i = 0; i < dataGrid.Columns.Count; i++)
                    {
                        var col = dataGrid.Columns[i];

                        // col.HeaderText ?? col.MappingName
                        GridCopyPasteCellEventArgs gridCopyPasteCellEventArgs = RaiseCopyGridCellContentEvent(col, null, col.HeaderText ?? col.MappingName);
                        if ((dataGrid.GridCopyOption.HasFlag(GridCopyOption.IncludeHiddenColumn) || !IsHiddenColumn(col)) && !gridCopyPasteCellEventArgs.Handled)
                        {
                            if (text.Length != 0)
                            {
                                text.Append('\t');
                            }

                            text = text.Append(gridCopyPasteCellEventArgs.ClipBoardValue);
                        }
                    }

                    text.Append("\r\n");
                }

                for (int j = 0; j < records.Count; j++)
                {
                    StringBuilder text2 = new StringBuilder();
                    CopyRow(records[j], ref text2);
                    text.Append(text2);
                    if (j < records.Count - 1)
                    {
                        text.Append("\r\n");
                    }
                }
            }
            protected override void CopyTextToClipBoard(ObservableCollection<object> records, bool cut)
            {
                if (RaiseCopyContentEvent(new GridCopyPasteEventArgs(handled: false, dataGrid)).Handled || records == null)
                {
                    return;
                }

                var order = dataGrid.View.Records.Select((x, i) => (x.Data, i)).ToDictionary(x => x.Data, x => x.i);

                SortedDictionary<int, object> sortedDictionary = new SortedDictionary<int, object>();
                for (int i = 0; i < records.Count; i++)
                {
                    if (order.TryGetValue(records[i], out var num))
                        sortedDictionary.Add(num, records[i]);
                }

                StringBuilder text = new StringBuilder();
                ObservableCollection<object> observableCollection = sortedDictionary.Values.ToObservableCollection();
                CopyRows(observableCollection, ref text);
                DataObject dataObject = new DataObject();
                if (text.Length > 0)
                {
                    dataObject.SetText(text.ToString());
                }

                if (cut)
                {
                    ClearCellsByCut(observableCollection);
                }

                Clipboard.SetDataObject(dataObject);
            }
            private static bool IsHiddenColumn(GridColumn col)
            {
                return (bool)col.GetType().GetMethod("IsHiddenColumn", BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!.Invoke(col, null)!;
            }
        }
    }

}
