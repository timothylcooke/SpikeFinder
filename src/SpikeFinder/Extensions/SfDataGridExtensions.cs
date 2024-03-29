﻿using ReactiveUI;
using Syncfusion.Data.Extensions;
using Syncfusion.UI.Xaml.Grid;
using System;
using System.Linq;
using System.Reactive.Linq;
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

            protected override GridCopyPasteCellEventArgs RaiseCopyGridCellContentEvent(GridColumn column, object rowData, object clipboardValue)
            {
                // Here, we fix a bug where they copy col.HeaderText to the clipboard, but they display column.HeaderText ?? column.MappingName in the header.
                // This only works because we never have null rows.
                if (rowData is null)
                    clipboardValue ??= column.MappingName;

                return base.RaiseCopyGridCellContentEvent(column, rowData, clipboardValue);
            }
        }
    }
}
