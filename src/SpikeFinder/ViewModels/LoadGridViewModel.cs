#nullable enable

using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SpikeFinder.Extensions;
using SpikeFinder.Models;
using SpikeFinder.SQLite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SpikeFinder.ViewModels
{
    public class LoadGridViewModel : SfViewModel
    {
        [Reactive] public ReadOnlyObservableCollection<LoadingItemViewModel>? LoadingItems { get; private set; }
        [Reactive] private long? TotalExams { get; set; }
        [Reactive] private bool IsAggregatingData { get; set; }


        private LoadingItemViewModel? _loadExamCountProgress, _loadBiometryValuesProgress, _loadDemographicsProgress, _loadMeasureModesAndWavelengthsProgress, _loadK1Progress, _loadK2Progress, _loadAxis1Progress, _loadWtwProgress, _loadIcxProgress, _loadIcyProgress, _loadPdProgress, _loadPcxProgress, _loadPcyProgress, _loadPersistedSpikes, _aggregateDataProgress;
        private Action<IDisposable>? _disposeDescription;

        public LoadGridViewModel()
        {
            this.WhenActivated(d =>
            {
                TotalExams = null;

                d(CreateSettingsCommand());

                d(InitializeLoadingItems());
                d(FetchData());
            });
        }

        private IDisposable InitializeLoadingItems()
        {
            var items = new[]
            {
                _loadExamCountProgress = new(1, "Counting how many exams we need to load…"),
                _loadBiometryValuesProgress = new(2, "Loading biometry values (CCT, AD, LT, VD, RT, AL)…"),
                _loadDemographicsProgress = new(3, "Loading demographics…"),
                _loadMeasureModesAndWavelengthsProgress = new(4, "Loading measurement modes and wavelengths…"),
                _loadK1Progress = new(5, "Loading K1 values…"),
                _loadK2Progress = new(6, "Loading K2 values…"),
                _loadAxis1Progress = new(7, "Loading Axis values…"),
                _loadWtwProgress = new(8, "Loading WTW values…"),
                _loadIcxProgress = new(9, "Loading ICX values…"),
                _loadIcyProgress = new(10, "Loading ICY values…"),
                _loadPdProgress = new(11, "Loading PD values…"),
                _loadPcxProgress = new(12, "Loading PCX values…"),
                _loadPcyProgress = new(13, "Loading PCY values…"),
                _loadPersistedSpikes = new(14, "Loading Persisted Spikes…"),
                _aggregateDataProgress = new(15, "Aggregating data…"),
            };

            CompositeDisposable disposables = new(items.Except(new[] { _loadPersistedSpikes }).Select(x => x.Initialize(this.WhenAnyValue(y => y.TotalExams))));
            _disposeDescription = d => d.DisposeWith(disposables);

            var sourceList = new SourceList<LoadingItemViewModel>().DisposeWith(disposables);
            sourceList.AddRange(items);

            Observable.Timer(DateTime.Now.AddSeconds(0.1), TimeSpan.FromSeconds(0.1)).Subscribe(_ => sourceList.Items.ToList().ForEach(x => x.SlowlyUpdatingProgress = x.ActualProgress)).DisposeWith(disposables);

            sourceList.Connect()
                .AutoRefresh(x => x.IsFinished)
                .Filter(x => !x.IsFinished)
                .Filter(this.WhenAnyValue(x => x.TotalExams).Select(x => new Func<LoadingItemViewModel, bool>(y => (x.HasValue != (y == _loadExamCountProgress)) && !y.IsFinished)))
                .Filter(this.WhenAnyValue(x => x.IsAggregatingData).Select(x => new Func<LoadingItemViewModel, bool>(y => x == (y == _aggregateDataProgress))))
                .Sort(SortExpressionComparer<LoadingItemViewModel>.Ascending(x => x.Step))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(out var visibleLoadingItems)
                .Subscribe()
                .DisposeWith(disposables);

            LoadingItems = visibleLoadingItems;

            return disposables;
        }
        private IDisposable FetchData()
        {
            CompositeDisposable disposables = new();

            var loadDataCommand = ReactiveCommand.CreateFromObservable(() => LoadLenstarExams(RxApp.TaskpoolScheduler).TakeUntil(HostScreen.Router.Navigate.Where(x => x != this))).DisposeWith(disposables);

            loadDataCommand.Select(x => new DataGridViewModel(x)).Cast<IRoutableViewModel>().InvokeCommand(HostScreen.Router.NavigateAndReset).DisposeWith(disposables);

            Observable.Return(Unit.Default).InvokeCommand(loadDataCommand).DisposeWith(disposables);

            return disposables;
        }

        private IObservable<List<LenstarExam>> LoadLenstarExams(IScheduler scheduler) => Observable.Zip(
            Observable.StartAsync(CountExams, scheduler).ObserveOnDispatcher().Do(x => TotalExams = x),

            Observable.StartAsync(token => LoadBiometryMeasurements(_loadBiometryValuesProgress, token), scheduler),
            Observable.StartAsync(token => LoadMeasureModesAndWavelengths(_loadMeasureModesAndWavelengthsProgress, token), scheduler),
            Observable.StartAsync(token => LoadExamDemographics(_loadDemographicsProgress, token), scheduler),

            Observable.StartAsync(token => LoadKeratometry(_loadK1Progress, KsValue.FlatK, token), scheduler),
            Observable.StartAsync(token => LoadKeratometry(_loadK2Progress, KsValue.SteepK, token), scheduler),
            Observable.StartAsync(token => LoadAxis1s(_loadAxis1Progress, token), scheduler),

            Observable.StartAsync(token => LoadWtwValue(_loadWtwProgress, WtwValue.WTW, token), scheduler),
            Observable.StartAsync(token => LoadWtwValue(_loadIcxProgress, WtwValue.ICX, token), scheduler),
            Observable.StartAsync(token => LoadWtwValue(_loadIcyProgress, WtwValue.ICY, token), scheduler),

            Observable.StartAsync(token => LoadPupilValue(_loadPdProgress, PupilValue.PD, token), scheduler),
            Observable.StartAsync(token => LoadPupilValue(_loadPcxProgress, PupilValue.PCX, token), scheduler),
            Observable.StartAsync(token => LoadPupilValue(_loadPcyProgress, PupilValue.PCY, token), scheduler),

            Observable.StartAsync(token => SQLiteDatabase.LoadPersistedSpikes(_loadPersistedSpikes!, _disposeDescription!, token), scheduler),

            (_, biometryMeasurements, measureModesAndWavelengths, demographics, k1, k2, axis1, wtw, icx, icy, pd, pcx, pcy, spikes) => AggregateLenstarExamData(demographics, biometryMeasurements, measureModesAndWavelengths, k1, k2, axis1, wtw, icx, icy, pd, pcx, pcy, spikes)
        ).SubscribeOn(scheduler);


        private static Task<Queue<T>> LoadValues<T>(LoadingItemViewModel? progress, Func<T, int> getExamId, Func<DbDataReader, T> readValue, string query, CancellationToken token)
        {
            if (progress == null)
            {
                return Task.FromResult(new Queue<T>());
            }
            else
            {
                int? lastId = null;
                var answers = new Queue<T>();

                return MySqlExtensions.RunSqlQuery(query, async reader =>
                {
                    while (await reader.ReadAsync(token))
                    {
                        T next;

                        try
                        {
                            next = readValue(reader);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Encountered exception while parsing results for the the following query:{Environment.NewLine}{Environment.NewLine}{query}", ex);
                        }

                        var nextId = getExamId(next);

                        if (nextId != lastId)
                        {
                            lastId = nextId;
                            progress.ActualProgress++;
                        }

                        answers.Enqueue(next);
                    }

                    progress.SlowlyUpdatingProgress = progress.ActualProgress = progress.MaxPossibleProgress ?? 1;
                }, () => answers, token);
            }
        }
        private static Task<Queue<SingleValueMeasurement>> LoadSingleValues(LoadingItemViewModel? progress, string query, CancellationToken token)
        {
            return LoadValues(progress, x => x.ExamId, reader => new SingleValueMeasurement(reader.GetInt32(0), (Eye)reader.GetByte(1), ValueWithStandardDeviation.FromValues(reader.GetDouble(2), reader.IsDBNull(3) ? new double?() : reader.GetDouble(3))), query, token);
        }

        private static Task<long> CountExams(CancellationToken token)
        {
            long totalExams = 0;

            return MySqlExtensions.RunSqlQuery("SELECT COUNT(*) FROM tbl_basic_patient pat JOIN tbl_basic_examination ex ON pat.pk_patient = ex.fk_patid WHERE ex.category = 1030;", async reader =>
            {
                if (!await reader.ReadAsync(token))
                    throw new Exception("Failed to count the total exams.");

                totalExams = reader.GetInt64(0);
            }, () => totalExams, token);
        }
        private static Task<Queue<BiometryMeasurements>> LoadBiometryMeasurements(LoadingItemViewModel? loadBiometryValues, CancellationToken token)
        {
            if (loadBiometryValues == null)
            {
                return Task.FromResult(new Queue<BiometryMeasurements>());
            }
            else
            {
                var biometryMeasurements = new Queue<BiometryMeasurements>();
                return MySqlExtensions.RunSqlQuery(@"SELECT meas.fk_examid, meas.eye, dimen.element, AVG(dimen.dimension * 1000), STDDEV_SAMP(dimen.dimension * 1000)
FROM tbl_bio_measurement meas
JOIN tbl_bio_biometry_dimensions dimen ON meas.pk_measurement = dimen.fk_measurement
WHERE dimen.used = 1 AND dimen.dimension >= -1
GROUP BY meas.fk_examid, meas.eye, dimen.element
ORDER BY meas.fk_examid, meas.eye, dimen.element;", async reader =>
                {
                    BiometryMeasurements? currentMeasurement = null;

                    void AddCurrentMeasurement()
                    {
                        if (currentMeasurement != null)
                        {
                            biometryMeasurements.Enqueue(currentMeasurement);
                        }
                    }

                    while (await reader.ReadAsync(token))
                    {
                        var examId = reader.GetInt32(0);
                        var eye = (Eye)reader.GetByte(1);

                        if (currentMeasurement?.ExamId != examId)
                        {
                            loadBiometryValues.ActualProgress++;
                        }

                        if (currentMeasurement?.ExamId != examId || currentMeasurement?.Eye != eye)
                        {
                            AddCurrentMeasurement();
                            currentMeasurement = new BiometryMeasurements { ExamId = examId, Eye = eye };
                        }

                        currentMeasurement[(Dimension)reader.GetByte(2)] = ValueWithStandardDeviation.FromValues(reader.GetDouble(3), await reader.IsDBNullAsync(4, token) ? new double?() : reader.GetDouble(4));
                    }

                    AddCurrentMeasurement();
                    loadBiometryValues.SlowlyUpdatingProgress = loadBiometryValues.ActualProgress = loadBiometryValues.MaxPossibleProgress ?? 1;
                }, () => biometryMeasurements, token);
            }
        }
        private static Task<Queue<MeasureModeAndWavelength>> LoadMeasureModesAndWavelengths(LoadingItemViewModel? loadDemographics, CancellationToken token)
        {
            return LoadValues(loadDemographics, x => x.ExamId, reader => new MeasureModeAndWavelength(reader.GetInt32(0), (Eye)reader.GetByte(1), (MeasureMode)reader.GetByte(2), reader.GetDouble(3)), @"SELECT meas.fk_examid, meas.eye, biom.meas_mode, setting.sld_wavelength * 1000000000 sld_wavelength
FROM tbl_bio_measurement meas
LEFT JOIN tbl_bio_biometry biom ON meas.pk_measurement = biom.fk_measurement
LEFT JOIN tbl_bio_biometry_setting_status setting ON biom.fk_biometry_setting_status = setting.pk_biometry_setting_status
WHERE biom.meas_mode IS NOT NULL
GROUP BY meas.fk_examid, meas.eye
ORDER BY meas.fk_examid, meas.eye;", token);
        }
        private static Task<Queue<ExamDemographics>> LoadExamDemographics(LoadingItemViewModel? loadDemographics, CancellationToken token)
        {
            return LoadValues(loadDemographics, x => x.ExamId, reader => new ExamDemographics(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetDateTime(3), reader.GetDateTime(4), reader.GetGuid(5), reader.GetInt32(6)), @"SELECT patient.patientid, patient.name, patient.firstname, patient.birthdate, exam.timestamp, exam.uuid, exam.pk_examination
FROM tbl_basic_patient patient
JOIN tbl_basic_examination exam ON patient.pk_patient = exam.fk_patid
WHERE exam.category = 1030
ORDER BY exam.pk_examination;", token);
        }
        private static Task<Queue<SingleValueMeasurement>> LoadKeratometry(LoadingItemViewModel? progress, KsValue ksValue, CancellationToken token)
        {
            var value = ksValue switch { KsValue.FlatK => "inner_radius_flat", KsValue.SteepK => "inner_radius_steep", _ => throw new ArgumentException(null, nameof(ksValue)) };
            var mask = ksValue switch { KsValue.FlatK => 1, KsValue.SteepK => 2, _ => throw new ArgumentException(null, nameof(ksValue)) };

            return LoadSingleValues(progress, $@"SELECT meas.fk_examid, meas.eye, AVG((setting.refractive_index - 1) / kera.{value}), STDDEV_SAMP((setting.refractive_index - 1) / kera.{value})
    FROM tbl_bio_measurement meas
    LEFT JOIN tbl_bio_keratometry kera ON meas.pk_measurement = kera.fk_measurement
    LEFT JOIN tbl_bio_keratometry_settings setting ON kera.fk_keratometry_settings = setting.pk_keratometry_settings
    WHERE kera.used & {mask} = {mask}
    GROUP BY meas.fk_examid, meas.eye
    ORDER BY meas.fk_examid, meas.eye;", token);
        }
        private static Task<Queue<SingleValueMeasurement>> LoadAxis1s(LoadingItemViewModel? loadAxis1, CancellationToken token)
        {
            return LoadValues(loadAxis1, x => x.ExamId, reader =>
            {
                var angle = ValueWithStandardDeviation.FromValues(reader.GetDouble(2), reader.IsDBNull(3) ? new double?() : reader.GetDouble(3));
                var angleOffset60 = ValueWithStandardDeviation.FromValues(reader.GetDouble(4), reader.IsDBNull(5) ? new double?() : reader.GetDouble(5));
                var angleOffset120 = ValueWithStandardDeviation.FromValues(reader.GetDouble(6), reader.IsDBNull(7) ? new double?() : reader.GetDouble(7));

                return new SingleValueMeasurement(reader.GetInt32(0), (Eye)reader.GetByte(1), angle.StandardDeviation <= angleOffset60.StandardDeviation && angle.StandardDeviation <= angleOffset120.StandardDeviation ? angle : (angleOffset60.StandardDeviation <= angleOffset120.StandardDeviation ? angleOffset60 : angleOffset120));
            }, @"SELECT meas.fk_examid, meas.eye, AVG(kera.inner_angle), stddev_samp(kera.inner_angle), (AVG((kera.inner_angle+60)%180)+120)%180, stddev_samp((kera.inner_angle+60)%180), (AVG((kera.inner_angle+120)%180)+60)%180, stddev_samp((kera.inner_angle+120)%180)
FROM tbl_bio_measurement meas
LEFT JOIN tbl_bio_keratometry kera ON meas.pk_measurement = kera.fk_measurement
WHERE kera.used & 4 = 4 AND kera.inner_angle >= -1
GROUP BY meas.fk_examid, meas.eye;", token);
        }
        private static Task<Queue<SingleValueMeasurement>> LoadWtwValue(LoadingItemViewModel? progress, WtwValue wtwValue, CancellationToken token)
        {
            var multiplier = wtwValue switch { WtwValue.WTW => 2000, _ => 1000 };
            var value = wtwValue switch { WtwValue.WTW => "iris_radius", WtwValue.ICX => "iris_center_x", WtwValue.ICY => "iris_center_y", _ => throw new ArgumentException(null, nameof(wtwValue)) };
            var mask = wtwValue switch { WtwValue.WTW => 32, WtwValue.ICX => 8, WtwValue.ICY => 16, _ => throw new ArgumentException(null, nameof(wtwValue)) };

            return LoadSingleValues(progress, $@"SELECT meas.fk_examid, meas.eye, AVG(wtw.{value}*{multiplier}), STDDEV_SAMP(wtw.{value}*{multiplier})
FROM tbl_bio_measurement meas
LEFT JOIN tbl_bio_whitewhite wtw ON meas.pk_measurement = wtw.fk_measurement
WHERE wtw.used & {mask} = {mask} AND wtw.{value} >= -1
GROUP BY meas.fk_examid, meas.eye
ORDER BY meas.fk_examid, meas.eye;", token);
        }
        private static Task<Queue<SingleValueMeasurement>> LoadPupilValue(LoadingItemViewModel? progress, PupilValue pupilValue, CancellationToken token)
        {
            var multiplier = pupilValue switch { PupilValue.PD => 2000, _ => 1000 };
            var value = pupilValue switch { PupilValue.PD => "pupil_radius", PupilValue.PCX => "pupil_center_x", PupilValue.PCY => "pupil_center_y", _ => throw new ArgumentException(null, nameof(pupilValue)) };
            var mask = pupilValue switch { PupilValue.PD => 32, PupilValue.PCX => 8, PupilValue.PCY => 16, _ => throw new ArgumentException(null, nameof(pupilValue)) };

            return LoadSingleValues(progress, $@"SELECT meas.fk_examid, meas.eye, AVG(pd.{value}*{multiplier}), STDDEV_SAMP(pd.{value}*{multiplier})
FROM tbl_bio_measurement meas
LEFT JOIN tbl_bio_pupilometry pd ON meas.pk_measurement = pd.fk_measurement
WHERE pd.used & {mask} = {mask} AND pd.{value} >= -1
GROUP BY meas.fk_examid, meas.eye
ORDER BY meas.fk_examid, meas.eye;", token);
        }

        private List<LenstarExam> AggregateLenstarExamData(Queue<ExamDemographics> demographics, Queue<BiometryMeasurements> biometryMeasurements, Queue<MeasureModeAndWavelength> measureModesAndWavelengths, Queue<SingleValueMeasurement> k1s, Queue<SingleValueMeasurement> k2s, Queue<SingleValueMeasurement> kAngles, Queue<SingleValueMeasurement> wtws, Queue<SingleValueMeasurement> icxs, Queue<SingleValueMeasurement> icys, Queue<SingleValueMeasurement> pds, Queue<SingleValueMeasurement> pcxs, Queue<SingleValueMeasurement> pcys, Dictionary<string, PersistedSpikes> spikes)
        {
            IsAggregatingData = true;
            var aggregatedData = new List<LenstarExam>();

            while (demographics.Count > 0)
            {
                var exam = demographics.Dequeue();

                bool hasOd = false, hasOs = false;

                FindLenstarExamParts(exam, biometryMeasurements, out var odBiometry, out var osBiometry, ref hasOd, ref hasOs);
                FindLenstarExamParts(exam, measureModesAndWavelengths, out var odMode, out var osMode, ref hasOd, ref hasOs);

                FindLenstarExamParts(exam, k1s, out var odK1, out var osK1, ref hasOd, ref hasOs);
                FindLenstarExamParts(exam, k2s, out var odK2, out var osK2, ref hasOd, ref hasOs);
                FindLenstarExamParts(exam, kAngles, out var odAxis1, out var osAxis1, ref hasOd, ref hasOs);

                FindLenstarExamParts(exam, wtws, out var odWtw, out var osWtw, ref hasOd, ref hasOs);
                FindLenstarExamParts(exam, icxs, out var odIcx, out var osIcx, ref hasOd, ref hasOs);
                FindLenstarExamParts(exam, icys, out var odIcy, out var osIcy, ref hasOd, ref hasOs);

                FindLenstarExamParts(exam, pds, out var odPd, out var osPd, ref hasOd, ref hasOs);
                FindLenstarExamParts(exam, pcxs, out var odPcx, out var osPcx, ref hasOd, ref hasOs);
                FindLenstarExamParts(exam, pcys, out var odPcy, out var osPcy, ref hasOd, ref hasOs);

                if (hasOd)
                {
                    aggregatedData.Add(AggregateLenstarExamData(exam, Eye.OD, odBiometry, odMode, odK1, odK2, odAxis1, odWtw, odIcx, odIcy, odPd, odPcx, odPcy, spikes));
                }

                if (hasOs)
                {
                    aggregatedData.Add(AggregateLenstarExamData(exam, Eye.OS, osBiometry, osMode, osK1, osK2, osAxis1, osWtw, osIcx, osIcy, osPd, osPcx, osPcy, spikes));
                }

                _aggregateDataProgress!.ActualProgress++;
            }

            return aggregatedData;
        }
        private static LenstarExam AggregateLenstarExamData(ExamDemographics demographics, Eye eye, BiometryMeasurements? biometry, MeasureModeAndWavelength? measureModeAndWavelength, SingleValueMeasurement? k1, SingleValueMeasurement? k2, SingleValueMeasurement? axis1, SingleValueMeasurement? wtw, SingleValueMeasurement? icx, SingleValueMeasurement? icy, SingleValueMeasurement? pd, SingleValueMeasurement? pcx, SingleValueMeasurement? pcy, Dictionary<string, PersistedSpikes> spikes)
            => new(demographics.Uuid, demographics.ExamId, eye, demographics.PatientNumber, demographics.LastName, demographics.FirstName, demographics.DOB, demographics.Timestamp, measureModeAndWavelength?.MeasureMode, measureModeAndWavelength?.Wavelength, biometry?.CCT, biometry?.AD, biometry?.LT, biometry?.VD, biometry?.RT, biometry?.AL, k1?.Value, k2?.Value, axis1?.Value, wtw?.Value, icx?.Value, icy?.Value, pd?.Value, pcx?.Value, pcy?.Value, spikes.TryGetValue(demographics.GetExamKey(eye), out var x) ? x : null);
        private static void FindLenstarExamParts<T>(ExamDemographics demographics, Queue<T> data, out T? od, out T? os, ref bool hasOd, ref bool hasOs)
            where T : ILenstarExamPart
        {
            if (data.TryPeek(out var first))
            {
                if (demographics.IsMatch(first))
                {
                    switch (first.Eye)
                    {
                        case Eye.OD:
                            hasOd = true;
                            od = data.Dequeue();

                            if (data.TryPeek(out var second) && demographics.IsMatch(second))
                            {
                                os = data.Dequeue();
                                hasOs = true;
                            }
                            else
                            {
                                os = default;
                            }

                            return;
                        case Eye.OS:
                            hasOs = true;
                            od = default;
                            os = data.Dequeue();
                            return;
                        default:
                            throw new ArgumentException($"{nameof(first)}.{nameof(first.Eye)} = {first.Eye}; expected OD or OS");
                    }
                }
            }

            od = os = default;
        }


        private enum KsValue
        {
            FlatK,
            SteepK
        }
        private enum WtwValue
        {
            WTW,
            ICX,
            ICY
        }
        private enum PupilValue
        {
            PD,
            PCX,
            PCY
        }

        public override string UrlPathSegment => "/Loading";
        public override string Title => "Loading Data…";


        private interface ILenstarExamPart
        {
            int ExamId { get; }
            Eye Eye { get; }
        }
        private record SingleValueMeasurement(int ExamId, Eye Eye, ValueWithStandardDeviation Value) : ILenstarExamPart;
        private record MeasureModeAndWavelength(int ExamId, Eye Eye, MeasureMode MeasureMode, double Wavelength) : ILenstarExamPart;
        private record ExamDemographics(string PatientNumber, string LastName, string FirstName, DateTime DOB, DateTime Timestamp, Guid Uuid, int ExamId)
        {
            public string GetExamKey(Eye eye) => LenstarExam.ComputeKey(Uuid, eye);
            public bool IsMatch(ILenstarExamPart examPart)
            {
                return ExamId == examPart.ExamId;
            }
        }
        private class BiometryMeasurements : ILenstarExamPart
        {
            public int ExamId { get; init; }
            public Eye Eye { get; init; }
            public ValueWithStandardDeviation? CCT { get; set; }
            public ValueWithStandardDeviation? AD { get; set; }
            public ValueWithStandardDeviation? LT { get; set; }
            public ValueWithStandardDeviation? VD { get; set; }
            public ValueWithStandardDeviation? RT { get; set; }
            public ValueWithStandardDeviation? AL { get; set; }

            public ValueWithStandardDeviation? this[Dimension dimension]
            {
                get => dimension switch
                {
                    Dimension.CCT => CCT,
                    Dimension.AD => AD,
                    Dimension.LT => LT,
                    Dimension.VD => VD,
                    Dimension.RT => RT,
                    Dimension.AL => AL,
                    _ => throw new ArgumentOutOfRangeException(nameof(dimension))
                };
                set
                {
                    switch (dimension)
                    {
                        case Dimension.CCT:
                            CCT = value;
                            break;
                        case Dimension.AD:
                            AD = value;
                            break;
                        case Dimension.LT:
                            LT = value;
                            break;
                        case Dimension.VD:
                            VD = value;
                            break;
                        case Dimension.RT:
                            RT = value;
                            break;
                        case Dimension.AL:
                            AL = value;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(dimension));
                    }
                }
            }
        }
    }
}
