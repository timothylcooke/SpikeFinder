#nullable enable

using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SpikeFinder.Models;
using SpikeFinder.SQLite;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SpikeFinder.ViewModels
{
    public class SpikesViewModel : SfViewModel
    {
        public LenstarExam Exam { get; }
        public double[] Spikes { get; }
        public double MaxValue { get; }
        public byte[] Image { get; }
        public LenstarCursorPositions Cursors { get; }

        public ObservableCollection<CursorPosition> SpikeControlCursors { get; }

        public override string? UrlPathSegment { get; }
        public override string Title { get; }

        [Reactive] public MeasureMode MeasureMode { get; set; }
        public object[] MeasureModes { get; }

        public string Notes { get; set; }

        public SpikesViewModel(LenstarExam exam, double[] spikes, double maxValue, byte[] image, LenstarCursorPositions cursors)
        {
            UrlPathSegment = $"/Spikes/{exam.Key}";
            Title = $"{exam.FirstName} {exam.LastName} (DOB {exam.DOB:d}; #{exam.PatientNumber}) {exam.Eye} measurement {exam.Timestamp:d}";

            MeasureModes = LenstarExam.GetMeasureModesWithDescription().Select(x => new { Value = x.Key, Description = x.Value }).ToArray();

            Exam = exam;
            Spikes = spikes;
            MaxValue = maxValue;
            Image = image;
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
