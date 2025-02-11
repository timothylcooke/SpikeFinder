using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SpikeFinder.Models;
using SpikeFinder.SQLite;
using Syncfusion.Data.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SpikeFinder.ViewModels
{
    public class SpikesViewModel : SfViewModel
    {
        public LenstarExam Exam { get; }
        [Reactive] public double[][] Spikes { get; private set; }
        [Reactive] public double[] MaxValue { get; private set; }
        [Reactive] public Geometry[][] Geometries { get; private set; }
        [Reactive] public bool Used { get; private set; }
        public LenstarCursorPositions Cursors { get; }

        private readonly Dictionary<string, long> _displayModes;
        public List<string> DisplayModes => _displayModes.Keys.ToList();
        [Reactive] public string DisplayMode { get; set; }

        public ObservableCollection<CursorPosition> SpikeControlCursors { get; }

        public override string? UrlPathSegment { get; }
        public override string Title { get; }

        [Reactive] public MeasureMode MeasureMode { get; set; }
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

                var renderableMeasurements = new[] { spikes[long.MinValue] }.Concat(Enumerable.Range(0, firstAscan - 1).Select(i => spikes[measurements[i]]));

                d(this.WhenAnyValue(x => x.DisplayMode)
                    .Select(x => _displayModes[x])
                    .Select(x => spikes.TryGetValue(x, out var s) ? [s] : x switch { long.MinValue + 1 => renderableMeasurements, long.MinValue + 2 => renderableMeasurements.Skip(1), _ => throw new Exception($"Invalid spike: {x}") })
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Do(x => Spikes = x.Select(y => y.Spikes).ToArray())
                    .Do(x => MaxValue = x.Select(y => y.MaxValue).ToArray())
                    .Do(x => Used = x.Select(y => y.Used).First())
                    .Do(x => Geometries = x.Select(spike =>
                    {
                        double X(int i) => i * Convert.ToDouble(LoadSpikesViewModel.ImageWidth) / spike.Spikes.Length;
                        double Y(int i) => LoadSpikesViewModel.ImageHeight - spike.Spikes[i] / spike.MaxValue * LoadSpikesViewModel.ImageHeight;

                        return Enumerable.Range(0, spike.Spikes.Length / 500).Select(i => Geometry.Parse(string.Concat("M", string.Join('L', Enumerable.Range(i * 500, Math.Min(501, spike.Spikes.Length - i * 500)).Select(i => string.Format(CultureInfo.InvariantCulture, "{0},{1}", X(i), Y(i))))))).ToArray();
                    }).ToArray())
                    .Subscribe()
                    );

                d(SaveCommand);
                d(NavigateBackCommand);
            });
        }

        private async Task SaveAsync(CancellationToken token)
        {
            await SQLiteDatabase.SaveSpikes(Exam.Key, new PersistedSpikes(SpikeControlCursors, Notes, MeasureMode), token);
        }
    }
}
