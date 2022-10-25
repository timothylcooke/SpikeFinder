using DynamicData;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SpikeFinder.Models;
using SpikeFinder.SQLite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;

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

            d(sourceCache);
        }

        [Reactive] public ReadOnlyObservableCollection<LenstarExam> Exams { get; private set; }
        [Reactive] public string SearchQuery { get; set; }

        public override string? Title => null;
        public override string UrlPathSegment => "/";
    }
}
