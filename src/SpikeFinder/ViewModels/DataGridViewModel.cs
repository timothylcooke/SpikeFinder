using DynamicData;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SpikeFinder.Models;
using SpikeFinder.RefractiveIndices;
using SpikeFinder.Settings;
using SpikeFinder.SQLite;
using Syncfusion.Data.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;

using static SpikeFinder.Models.LensMaterial;

namespace SpikeFinder.ViewModels
{
    public class DataGridViewModel : SfViewModel
    {
        public DataGridViewModel(List<LenstarExam> exams)
        {
            SearchQuery = "";

            // Here, we intentionally don't call WhenActivated, in order to speed up activation. We only ever create one DataGridViewModel, and there's no reason to recreate the cache/filter each time we navigate to/from a scan.
            static void d(IDisposable _) { }

            d(CreateSettingsCommand());

            var sourceCache = new SourceCache<LenstarExam, string>(x => x.Key);

            d(SQLiteDatabase.SpikesSaved.Subscribe(x => sourceCache.AddOrUpdate(sourceCache.Lookup(x.examKey).Value with { PersistedSpikes = x.spikes })));

            IObservable<Func<LenstarExam, bool>> filter =
                this.WhenAnyValue(x => x.SearchQuery)
                    .ObserveOn(RxApp.TaskpoolScheduler)
                    .WhereNotNull()
                    .Select(x => x.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    .Throttle(TimeSpan.FromMilliseconds(300))
                    .Select(queryWords => new Func<LenstarExam, bool>(exam => queryWords.All(exam.IsMatch)));

            d(sourceCache.Connect()
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Filter(filter)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(out var filteredExams)
                .Subscribe());

            Exams = filteredExams;

            sourceCache.AddOrUpdate(exams);

            d(SfMachineSettings.Instance.WhenAnyValue(x => x.RefractiveIndexMethod).DistinctUntilChanged()
                .Skip(1)
                .Select(_ => Exams.Where(x => x.HasSpikes))
                .Subscribe(x => x.ForEach(y => y.OnRefractiveIndexMethodChanged())));

            d(SfMachineSettings.Instance.WhenAnyValue(x => x.PseudophakicDefaultRefractiveIndex, x => x.PseudophakicAcrylicRefractiveIndex, x => x.PseudophakicPMMARefractiveIndex, x => x.PseudophakicSiliconeRefractiveIndex).DistinctUntilChanged()
                .Skip(1)
                .Select(_ => Exams.Where(x => x.HasSpikes && x.MeasureMode is { } mm &&
                    RefractiveIndexMethod.GetLensMaterial(mm) is PseudophakicAcrylic or PseudophakicDefault or PseudophakicPMMA or PseudophakicSilicone))
                .Subscribe(x => x.ForEach(y => y.OnLensRefractiveIndexChanged())));

            d(SfMachineSettings.Instance.WhenAnyValue(x => x.SiliconeOilRefractiveIndex).DistinctUntilChanged()
                .Skip(1)
                .Select(_ => Exams.Where(x => x.HasSpikes && x.MeasureMode is { } mm && RefractiveIndexMethod.GetVitreousMaterial(mm) is VitreousMaterial.SiliconeOil))
                .Subscribe(x => x.ForEach(y => y.OnVitreousRefractiveIndexChanged())));

            d(SfMachineSettings.Instance.WhenAnyValue(x => x.RetinaRefractiveIndex).DistinctUntilChanged()
                .Skip(1)
                .Select(_ => Exams.Where(x => x.HasSpikes))
                .Subscribe(x => x.ForEach(y => y.OnRetinaRefractiveIndexChanged())));

            d(sourceCache);
        }

        [Reactive] public ReadOnlyObservableCollection<LenstarExam> Exams { get; private set; }
        [Reactive] public string SearchQuery { get; set; }

        public override string? Title => null;
        public override string UrlPathSegment => "/";
    }
}
