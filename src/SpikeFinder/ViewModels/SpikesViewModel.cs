using Microsoft.Win32;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using SpikeFinder.Extensions;
using SpikeFinder.Models;
using SpikeFinder.SQLite;
using SpikeFinder.Views;
using Syncfusion.Data.Extensions;
using Syncfusion.XlsIO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace SpikeFinder.ViewModels
{
    public partial class SpikesViewModel : SfViewModel
    {
        public LenstarExam Exam { get; }
        [Reactive(SetModifier = AccessModifier.Private)] private double[][]? _spikes;
        [Reactive(SetModifier = AccessModifier.Private)] private double[]? _maxValue;
        [Reactive(SetModifier = AccessModifier.Private)] private Geometry[][]? _geometries;
        [Reactive(SetModifier = AccessModifier.Private)] private bool _used;
        [Reactive(SetModifier = AccessModifier.Private)] private bool _isNotExporting;
        public LenstarCursorPositions Cursors { get; }

        private readonly Dictionary<string, long> _displayModes;
        public List<string> DisplayModes => _displayModes.Keys.ToList();
        [Reactive] private string _displayMode;

        public ObservableCollection<CursorPosition> SpikeControlCursors { get; }

        public override string? UrlPathSegment { get; }
        public override string Title { get; }

        [Reactive] private MeasureMode _measureMode;
        public object[] MeasureModes { get; }

        public string Notes { get; set; }

        public SpikesViewModel(LenstarExam exam, IDictionary<long, RenderableSpike> spikes, LenstarCursorPositions cursors)
        {
            UrlPathSegment = $"/Spikes/{exam.Key}";
            Title = $"{exam.FirstName} {exam.LastName} (DOB {exam.DOB:d}; #{exam.PatientNumber}) {exam.Eye} measurement {exam.Timestamp:d}";

            MeasureModes = LenstarExam.GetMeasureModesWithDescription().Select(x => new { Value = x.Key, Description = x.Value }).ToArray();

            Exam = exam;

            DisplayMode = "Aggregate Scan Only";

            _displayModes = new Dictionary<string, long> {
                { DisplayMode, long.MinValue },
                { "Aggregate + All measurements", long.MinValue + 1 },
                { "All measurements", long.MinValue + 2 },
            };

            var measurements = spikes.Keys.Where(x => x is >= 0).OrderBy(x => x).ToList();

            var lastPossibleMeasurement = (measurements[0] << 4) - 1;
            var firstAscan = measurements.FindIndex(1, x => x > lastPossibleMeasurement);

            Enumerable.Range(0, firstAscan).ForEach(i => _displayModes[$"Measurement {i + 1}"] = measurements[i]);
            Enumerable.Range(0, firstAscan).ForEach(i => Enumerable.Range(0, 16).Select(ascan => (display: $"Measurement {i + 1}: Ascan {ascan + 1}", value: (measurements[i] << 4) + ascan)).Where(x => spikes.ContainsKey(x.value)).ForEach(x => _displayModes[$"{x.display}{(spikes[x.value].Used ? null : " (Not Used)")}"] = x.value));

            Cursors = cursors;
            MeasureMode = exam.MeasureMode ?? MeasureMode.PHAKIC;
            Notes = exam.PersistedSpikes?.Notes ?? "";

            SpikeControlCursors = new ObservableCollection<CursorPosition>(
                    from x in new(int? x, CursorElement element)[] { (cursors.PosteriorCornea, CursorElement.PosteriorCornea), (cursors.AnteriorLens, CursorElement.AnteriorLens), (cursors.PosteriorLens, CursorElement.PosteriorLens), (cursors.RPE - 350 ?? cursors.ILM, CursorElement.ILM), (cursors.RPE ?? cursors.ILM + 350, CursorElement.RPE) }
                    select new CursorPosition { X = x.x, DisplayName = typeof(CursorElement).GetField(typeof(CursorElement).GetEnumName(x.element)!)!.GetCustomAttributes<DescriptionAttribute>().Single().Description, CursorElement = x.element }
                );

            this.WhenActivated(d =>
            {
                SaveCommand = ReactiveCommand.CreateFromObservable(() => Observable.StartAsync(SaveAsync));
                NavigateBackCommand = ReactiveCommand.CreateFromObservable(HostScreen.Router.NavigateBack.Execute, HostScreen.Router.NavigateBack.CanExecute.CombineLatest(SaveCommand.IsExecuting, (canNavigateBack, isSaving) => canNavigateBack && !isSaving));

                d(SaveCommand.InvokeCommand(HostScreen.Router.NavigateBack));

                var renderableMeasurements = new[] { spikes[long.MinValue] }.Concat(Enumerable.Range(0, firstAscan).Select(i => spikes[measurements[i]]));

                ExportCommand = ReactiveCommand.CreateFromObservable(() =>
                    Observable.Return(new ChooseExportRangeChildWindow(renderableMeasurements.Skip(1).Select(Render).ToArray()))
                        .SelectMany(x => App.SpikeFinderMainWindow.ShowChildWindow<ExportRange?>(x).WhereNotNull())
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Select(exportRange => (exportRange, dialog: new SaveFileDialog { Filter = "Excel files|*.xlsx|All files|*.*", DefaultExt = "xlsx", Title = "Where should we save it?" }))
                        .Where(x => x.dialog.ShowDialog(Application.Current.MainWindow) == true)
                        .Select(x => (x.exportRange with { SaveAs = x.dialog.FileName })));

                d(ExportCommand.IsExecuting.Invert().BindTo(this, x => x.IsNotExporting));
                d(ExportCommand.Select(x => (x.SaveAs, measurements: renderableMeasurements.Select(y => (firstIndex: (int)Math.Floor(x.Min * y.Spikes.Length), lastIndex: (int)Math.Ceiling(x.Max * y.Spikes.Length), y.Original, y.Spikes)).ToList()))
                    .Do(x =>
                    {
                        using var excel = new ExcelEngine();

                        excel.Excel.DefaultVersion = ExcelVersion.Excel2016;

                        var sheetNames = new[] { "Raw Data" }.Concat(x.measurements.Select((_, i) => $"Measurement {i + 1}")).ToArray();

                        var workbook = excel.Excel.Workbooks.Create(sheetNames);

                        var sheet = workbook.Worksheets[0];

                        sheet.Range["B2"].Value = "Wavelength (nm)";
                        sheet.Range["B3"].Value2 = Exam.Wavelength;

                        sheet.Range["C2"].Value = "First Name";
                        sheet.Range["C3"].Value2 = Exam.FirstName;

                        sheet.Range["D2"].Value = "Last Name";
                        sheet.Range["D3"].Value2 = Exam.LastName;

                        sheet.Range["E2"].Value = "Timestamp";
                        sheet.Range["E3"].Value2 = Exam.Timestamp;
                        sheet.Range["E3"].NumberFormat = $"{CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern} {CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern}".Replace("tt", "AM/PM");

                        sheet.Range["F2"].Value = "Unique ID";
                        sheet.Range["F3"].Value2 = Exam.Key;

                        var position = x.measurements[0].firstIndex > 1000 ? "posterior" : "anterior";
                        sheet.Range["G2"].Value = $"First point is more {position} than the anterior corneal spike (measured in millimeters in air thickness):";
                        sheet.Range["G3"].Value2 = Math.Abs((x.measurements[0].firstIndex - 1000) / 1250.0);

                        Enumerable.Range(0, x.measurements.Count)
                            .ForEach(m =>
                            {
                                var meas = x.measurements[m];

                                sheet.Range[$"{(char)('B' + m)}5"].Value = m switch { 0 => "Aggregate Measurement (Original)", _ => $"Measurement {m} (Original)" };
                                Enumerable.Range(meas.firstIndex, meas.lastIndex - meas.firstIndex)
                                    .ForEach(r => sheet.Range[$"{(char)('B' + m)}{6 + r - meas.firstIndex}"].Value2 = meas.Original[r]);

                                sheet.Range[$"{(char)('B' + x.measurements.Count + 1 + m)}5"].Value = m switch { 0 => "Aggregate Measurement (SpikeFinder)", _ => $"Measurement {m + 1} (SpikeFinder)" };
                                Enumerable.Range(meas.firstIndex, meas.lastIndex - meas.firstIndex)
                                    .ForEach(r => sheet.Range[$"{(char)('B' + x.measurements.Count + 1 + m)}{6 + r - meas.firstIndex}"].Value2 = meas.Spikes[r]);
                            });

                        sheet.Range[1, 2, 1, Math.Max(7, (x.measurements.Count + 1) * 2)].Columns.ForEach(x => x.ColumnWidth = 25);

                        workbook.SaveAs(x.SaveAs);
                    })
                    .CatchAndShowErrors(true)
                    .Subscribe());

                d(this.WhenAnyValue(x => x.DisplayMode)
                    .Select(x => _displayModes[x])
                    .Select(x => spikes.TryGetValue(x, out var s) ? [s] : x switch { long.MinValue + 1 => renderableMeasurements, long.MinValue + 2 => renderableMeasurements.Skip(1), _ => throw new Exception($"Invalid spike: {x}") })
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Do(x => Spikes = x.Select(y => y.Spikes).ToArray())
                    .Do(x => MaxValue = x.Select(y => y.MaxValue).ToArray())
                    .Do(x => Used = x.Select(y => y.Used).First())
                    .Do(x => Geometries = x.Select(Render).ToArray())
                    .Subscribe()
                    );

                d(SaveCommand);
                d(NavigateBackCommand);
            });
        }

        private Geometry[] Render(RenderableSpike spike)
        {
            double X(int i) => i * Convert.ToDouble(LoadSpikesViewModel.ImageWidth) / spike.Spikes.Length;
            double Y(int i) => LoadSpikesViewModel.ImageHeight - spike.Spikes[i] / spike.MaxValue * LoadSpikesViewModel.ImageHeight;

            return Enumerable.Range(0, spike.Spikes.Length / 500).Select(i => Geometry.Parse(string.Concat("M", string.Join('L', Enumerable.Range(i * 500, Math.Min(501, spike.Spikes.Length - i * 500)).Select(i => string.Format(CultureInfo.InvariantCulture, "{0},{1}", X(i), Y(i))))))).ToArray();
        }

        private async Task SaveAsync(CancellationToken token)
        {
            await SQLiteDatabase.SaveSpikes(Exam.Key, new PersistedSpikes(SpikeControlCursors, Notes, MeasureMode), token);
        }
    }
}
