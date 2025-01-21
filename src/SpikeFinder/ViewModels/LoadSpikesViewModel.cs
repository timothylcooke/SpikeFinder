using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using MySqlConnector;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SpikeFinder.Extensions;
using SpikeFinder.Models;
using Syncfusion.Data.Extensions;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SpikeFinder.ViewModels
{
    public class LoadSpikesViewModel : SfViewModel
    {
        public const int ImageHeight = 1200;
        public const int ImageWidth = 4800;
        const double spikesPower = 1.8;

        public LoadSpikesViewModel(LenstarExam exam)
        {
            UrlPathSegment = $"/LoadSpikes/{exam.Key}";
            Title = $"Loading {exam.FirstName} {exam.LastName} (DOB {exam.DOB:d}; #{exam.PatientNumber}) {exam.Eye} measurement {exam.Timestamp:d}…";

            var loadingItemCount = new LoadingItemViewModel(1, "Figuring out much stuff there is to load…");
            var loadingCursors = new LoadingItemViewModel(2, "Loading Cursor positions…");
            var loadingSpikeData = new LoadingItemViewModel(3, "Loading Spike Data…");
            LoadingItems = [loadingItemCount, loadingSpikeData, loadingCursors];

            this.WhenActivated(d =>
            {
                d(CountTotalScans(exam, RxApp.TaskpoolScheduler)
                    .ToPropertyEx(this, x => x.MeasurementsToRead));

                // reading and decompressing are done once per ascan.
                new[] { loadingItemCount, loadingSpikeData }.ForEach(x => d(x.Initialize(this.WhenAnyValue(y => y.MeasurementsToRead, y => y.TotalScans))));

                // There are six cursors, six dimensions, and one biometry per measurement. So 13 items to read.
                d(loadingCursors.Initialize(exam.PersistedSpikes == null ? this.WhenAnyValue(y => y.MeasurementsToRead).Select(y => y.Measurements * 13) : Observable.Return<long?>(0)));

                // Mark loadingItemCount as finished as soon as we load the total ascan data count.
                d(this.WhenAnyValue(y => y.MeasurementsToRead).Where(x => x.TotalScans.HasValue).Take(1).ObserveOn(RxApp.MainThreadScheduler).Subscribe(x => loadingItemCount.SlowlyUpdatingProgress = x.TotalScans!.Value));

                var readAndDecompressSpikes =
                    // Fetch the spike data from MySQL
                    ReadSpikes(exam, this.WhenAnyValue(x => x.MeasurementsToRead).Where(x => x.TotalScans is > 0).Select(x => (x.ExamId, x.Version)))
                    .Do(x => Debug.WriteLine($"Found scandata: {x.MeasurementId}/{x.Index}"))

                    // Decompress the scan data asynchronously, on as many threads as we have processors
                    .Select(x =>
                        Observable.DeferAsync(async token =>
                            Observable.Return(await Task.Run(() => x.DecompressAsync(token), token), RxApp.TaskpoolScheduler)
                                .ObserveOn(RxApp.MainThreadScheduler)
                                .Do(_ => loadingSpikeData.SlowlyUpdatingProgress++)
                                .ObserveOn(RxApp.TaskpoolScheduler))
                    )
                    .Merge(Environment.ProcessorCount)

                    // Merge DecompressedScans together by MeasurementId
                    .GroupBy(x => x.MeasurementId, x => x.DecompressedScan)

                    // Within each MeasurementId, sum all of the different float arrays
                    .SelectMany(x =>
                            Observable.Return(x.Key)
                            .CombineLatest(x.Count(), x.Aggregate(SumDecompressedScans), (measurementId, count, spikes) => (measurementId, count, spikes))
                    )

                    // Don't run everything above twice.
                    .Replay()
                    .RefCount();

                // Here, we aggregate them into one single spike and prepare them for rendering
                var mergeSpikes = readAndDecompressSpikes
                        .Select(x => x.spikes)
                        .Aggregate(SumDecompressedScans)
                        .CombineLatest(readAndDecompressSpikes.Sum(x => x.count), TransformSpikesForRender)
                        .Select(x => (MeasurementId: -1, Spikes: x));

                IObservable<LenstarCursorPositions> cursors;

                if (exam.PersistedSpikes == null)
                {
                    var readCursors = ReadCursors(exam, RxApp.TaskpoolScheduler).Replay();

                    var readDimensions = ReadDimensions(exam, RxApp.TaskpoolScheduler).Replay();

                    var readBiometry = ReadBiometryData(exam, RxApp.TaskpoolScheduler).Replay();

                    // These are already sorted by MeasurementId.
                    // The zip will implicitly be a Join on MeasurementId.
                    cursors = Observable.Zip(
                            readCursors.GroupBy(x => x.MeasurementId)
                                .SelectMany(async x => (MeasurementId: x.Key, Cursors: await x.ToDictionary(y => y.Cursor)))
                                .ToDictionary(x => x.MeasurementId, x => x.Cursors),

                            readDimensions.GroupBy(x => x.MeasurementId)
                                .SelectMany(async x => (MeasurementId: x.Key, SegmentLengths: await x.ToDictionary(y => y.Element)))
                                .ToDictionary(x => x.MeasurementId, x => x.SegmentLengths),

                            readBiometry.ToDictionary(x => x.MeasurementId, x => x.Algorithm),

                            (c, d, b) => b.Select(x => (Cursors: c[x.Key], IsSelected: x.Value == Algorithm.Composite || d[x.Key].Any(y => y.Value.Used), Algorithm: x.Value, IsValueSelected: new Func<Dimension, bool>(y => d[x.Key][y].Used)))
                        )
                        .SelectMany(x => x)
                        .Select(x => (x.Cursors, x.Algorithm, IsCursorUsable: new Func<CursorElement, bool>(y => y switch
                        {
                            CursorElement.AnteriorCornea => x.IsSelected,
                            CursorElement.PosteriorCornea => x.IsValueSelected(Dimension.CCT) || x.IsValueSelected(Dimension.AD),
                            CursorElement.AnteriorLens => x.IsValueSelected(Dimension.AD) || x.IsValueSelected(Dimension.LT),
                            CursorElement.PosteriorLens => x.IsValueSelected(Dimension.LT),
                            CursorElement.ILM => x.IsValueSelected(Dimension.AL),
                            CursorElement.RPE => x.IsValueSelected(Dimension.AL) || x.IsValueSelected(Dimension.RT),
                            _ => false
                        })))
                        .Select(x => (Cursors: x.Cursors.ToDictionary(y => y.Key, y => (Usable: x.IsCursorUsable(y.Key), y.Value.ScanPos)), x.Algorithm))
                        .ToList()
                        .Select(x =>
                        {
                            var showCursors = new[] { CursorElement.AnteriorCornea, CursorElement.PosteriorCornea, CursorElement.AnteriorLens, CursorElement.PosteriorLens, CursorElement.ILM, CursorElement.RPE };

                            var nonCompositeMeasurements = x.Where(y => y.Algorithm != Algorithm.Composite).ToList();

                            return showCursors.Select(Cursor => (Cursor, Cursors: (Cursor is CursorElement.ILM or CursorElement.RPE ? x : nonCompositeMeasurements).Select(y => y.Cursors[Cursor]).ToList()))
                                .ToDictionary(x => x.Cursor, x =>
                                {
                                    var usable = x.Cursors.Where(y => y.Usable).ToList();
                                    return usable.Any() ? (int)Math.Round(usable.Average(y => y.ScanPos)) : new int?();
                                });
                        })
                        .CatchAndShowErrors()
                        .Select(x => new LenstarCursorPositions(x[CursorElement.AnteriorCornea], x[CursorElement.PosteriorCornea], x[CursorElement.AnteriorLens], x[CursorElement.PosteriorLens], x[CursorElement.ILM], x[CursorElement.RPE]));


                    //Increment progress on the main thread.
                    d(readCursors.Select(_ => Unit.Default)
                        .Merge(readDimensions.Select(_ => Unit.Default))
                        .Merge(readBiometry.Select(_ => Unit.Default))
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(_ => loadingCursors.SlowlyUpdatingProgress++));

                    // average all cursors.
                    //cursors =
                    //    // In order to keep the cursor, it must have a positive value and it must have a greater position than the previous cursor.
                    //    // We use zip to combine the cursor with the one before it, then we artificially set the Value to 0 if it has the same position as the previous cursor.
                    //    // Then, we filter the cursors where Value <= 0.
                    //    readCursors.StartWith(new CursorMarker[1])
                    //    .Zip(readCursors, (previousCursor, cursor) => (cursor.MeasurementId != previousCursor?.MeasurementId || cursor.ScanPos > previousCursor.ScanPos) ? cursor : cursor with { Value = 0 })
                    //    .Where(x => x.Value > 0)

                    //    // If it has the same value as the cursor before it, throw it out.
                    //    .GroupBy(x => x.Cursor)
                    //    .SelectMany(x => Observable.Return(x.Key)
                    //        .CombineLatest(x
                    //            .Select(y => y.ScanPos)
                    //            .Average(),
                    //                (cursor, scanPos) => new CursorMarker(-1, cursor, scanPos, 1000)))
                    //    .ToList()
                    //    .Select(CombineCursors);

                    d(readDimensions.Connect());
                    d(readCursors.Connect());
                    d(readBiometry.Connect());
                }
                else
                {
                    var s = exam.PersistedSpikes;
                    cursors = Observable.Return(new LenstarCursorPositions(1000, s.PosteriorCornea, s.AnteriorLens, s.PosteriorLens, s.ILM, s.RPE));
                }

                d(mergeSpikes
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Select(x => (x.Spikes.MaxValue, x.Spikes.Spikes, X: new Func<int, double>(y => y * Convert.ToDouble(ImageWidth) / x.Spikes.Spikes.Length), Y: new Func<double, double>(y => ImageHeight - y / x.Spikes.MaxValue * ImageHeight)))
                    .Select(x => (x.MaxValue, x.Spikes, Geometries: Enumerable.Range(0, x.Spikes.Length / 500).Select(i => Geometry.Parse(string.Concat("M", string.Join('L', Enumerable.Range(i * 500, Math.Min(501, x.Spikes.Length - i * 500)).Select(i => string.Format(CultureInfo.InvariantCulture, "{0},{1}", x.X(i), x.Y(x.Spikes[i]))))))).ToArray()))
                    .CombineLatest(cursors, (rendered, cursors) => new SpikesViewModel(exam, rendered.Spikes, rendered.MaxValue, rendered.Geometries, cursors))
                    .Cast<IRoutableViewModel>()
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .CatchAndShowErrors()
                    .Select(x => HostScreen.Router.NavigateBack.Execute().Select(_ => x))
                    .Switch()
                    .WhereNotNull()
                    .InvokeCommand(HostScreen.Router.Navigate));


                //d(renderedSpikes.Select(x => x[-1])
                //    .CombineLatest(cursors, (renderedSpikes, cursors) => (renderedSpikes, cursors))
                //    .Select(x => new SpikesViewModel(exam, x.renderedSpikes.Spikes.Spikes, x.renderedSpikes.Spikes.MaxValue, x.renderedSpikes.Image, x.cursors))
                //    .Cast<IRoutableViewModel>()
                //    .ObserveOnDispatcher()
                //    .Select(x => HostScreen.Router.NavigateBack.Execute().Select(_ => x))
                //    .Switch()
                //    .InvokeCommand(HostScreen.Router.Navigate));
            });
        }

        [ObservableAsProperty] private (long? Measurements, long? TotalScans, string? ExamId, int Version) MeasurementsToRead { get; }
        public IEnumerable<LoadingItemViewModel>? LoadingItems { get; }

        private static LenstarCursorPositions CombineCursors(IList<CursorMarker> cursors)
        {
            return new LenstarCursorPositions(
                CursorPosition(cursors, CursorElement.AnteriorCornea),
                CursorPosition(cursors, CursorElement.PosteriorCornea),
                CursorPosition(cursors, CursorElement.AnteriorLens),
                CursorPosition(cursors, CursorElement.PosteriorLens),
                CursorPosition(cursors, CursorElement.ILM),
                CursorPosition(cursors, CursorElement.RPE)
            );
        }
        private static int? CursorPosition(IList<CursorMarker> cursors, CursorElement element)
        {
            return cursors.SingleOrDefault(x => x.Cursor == element)?.ScanPos is { } position ? (int)Math.Round(position) : new int?();
        }

        private static Action<MySqlCommand> AddSqlParameters(LenstarExam exam) => cmd =>
        {
            cmd.Parameters.AddWithValue("@ExamId", exam.ExamId);
            cmd.Parameters.AddWithValue("@Eye", (byte)exam.Eye);
        };

        private static IObservable<(long? Measurements, long? TotalScans, string? , int version)> CountTotalScans(LenstarExam exam, IScheduler scheduler)
        {
            IObservable<(long?, long?, string?, int)> CountTable(string ascanTable, int version) =>
                MySqlExtensions.Connect<(long?, long?, string?, int)>(async (cmd, obs) =>
            {
                cmd.CommandText = $@"SELECT COUNT(measurements.pk_measurement) TotalMeasurements, IFNULL(SUM(n), 0) TotalAscans, MAX(uuid) uuid FROM (
    SELECT exam.uuid, meas.pk_measurement, COUNT(ascan.used) n
    FROM tbl_basic_examination exam
    JOIN tbl_bio_measurement meas ON meas.fk_examid = exam.pk_examination
    LEFT JOIN {ascanTable} ascan ON ascan.fk_measurement = meas.pk_measurement AND ascan.used = 1
    WHERE exam.pk_examination = @ExamId AND meas.eye = @Eye
    GROUP BY meas.pk_measurement
) measurements;";

                cmd.Parameters.AddWithValue("ExamId", exam.ExamId);
                cmd.Parameters.AddWithValue("Eye", (byte)exam.Eye);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        obs.OnNext((reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), version));
                    }
                    else
                    {
                        obs.OnError(new Exception("Failed to count exams."));
                    }
                }
            });

            return CountTable("tbl_bio_ascan", 0)
                .SelectMany(x => x switch {

                    // If there aren't any tbl_bio_ascan, let's try tbl_bio_ascan2.
                    (_, 0, _, 0) => CountTable("tbl_bio_ascan2", 1),

                    // If we actually get a count, we continue.
                    _ => Observable.Return(x)
            });
        }

        private class BlobMap
        {
            private BlobMap(Dictionary<string, ByteRange> blobs, byte[] bin)
            {
                _blobs = blobs;
                _bin = bin;
            }
            private readonly Dictionary<string, ByteRange> _blobs;
            private readonly byte[] _bin;
            public static IObservable<BlobMap?> FromExamId(string examid) =>
                Observable.StartAsync(async ct =>
                {
                    var id = examid!.Replace("-", "");

                    var path = Path.Join([@"C:\EyeSuiteFileStorage", .. Enumerable.Range(0, id.Length / 2).Select(i => id.Substring(i * 2, 2)), $"bio1_{examid}.esb00"]);

                    using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

                    using var sr = new StreamReader(fs);

                    Dictionary<string, ByteRange> blobs = new();
                    var regex = new Regex(@"^(\S+)\s(\d+)\s(\d+)$", RegexOptions.Compiled);
                    int headerLength = 0;

                    while (true)
                    {
                        switch (await sr.ReadLineAsync(ct))
                        {
                            case "":
                                headerLength += 3; // \r\n\0
                                fs.Position = headerLength;
                                var b = new byte[fs.Length - fs.Position - 8]; // Last 8 = checksum
                                await fs.ReadExactlyAsync(b, 0, b.Length, ct);
                                return new BlobMap(blobs, b);

                            case string line:
                                if (regex.Match(line) is { Success: true, Groups: [{ }, { Value: { } key }, { Value: { } start }, { Value: { } len }] })
                                    blobs[key] = new(int.Parse(start), int.Parse(len));
                                else
                                    throw new Exception("Blob storage is corrupt.");

                                headerLength += line.Length + 2;
                                break;

                            case null:
                                throw new Exception("Blob storage is corrupt.");
                        }
                    }
                });
            private record ByteRange(int start, int length);

            public byte[] GetBlob(string key)
            {
                var index = _blobs[key];
                var bytes = new byte[index.length];

                Array.Copy(_bin, index.start, bytes, 0, bytes.Length);

                return bytes;
            }
        }

        private static IObservable<CompressedSpikes> ReadSpikes(LenstarExam exam, IObservable<(string? examid, int version)> examInfo) =>
            examInfo
                .Take(1)
                .SelectMany(x => x.version switch
                {
                    0 => Observable.Return((x.version, blobMap: (BlobMap?)null)),
                    _ => BlobMap.FromExamId(x.examid!).Select(blobMap => (x.version, blobMap))
                })
                .SelectMany(x => MySqlExtensions.Connect<CompressedSpikes>(async (cmd, obs) =>
                {
                    cmd.CommandText = $@"SELECT ascan.fk_measurement, ascan.idx, ascan.scan_length{(x.version == 0 ? ", ascan.scandata" : null)}
    FROM tbl_bio_measurement meas
    JOIN {(x.version == 0 ? "tbl_bio_ascan" : "tbl_bio_ascan2")} ascan ON ascan.fk_measurement = meas.pk_measurement
    WHERE meas.fk_examid = @ExamId AND meas.eye = @Eye AND ascan.used = 1
    ORDER BY ascan.fk_measurement, ascan.idx;";

                    cmd.Parameters.AddWithValue("ExamId", exam.ExamId);
                    cmd.Parameters.AddWithValue("Eye", (byte)exam.Eye);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            obs.OnNext(new CompressedSpikes(reader.GetInt32(0), reader.GetByte(1), reader.GetInt32(2), x.version == 0 ? (byte[])reader[3] : x.blobMap!.GetBlob($"{reader.GetInt32(0)}-a-{reader.GetByte(1)}")));
                        }
                    }
                }));
        private static IObservable<CursorMarker> ReadCursors(LenstarExam exam, IScheduler scheduler)
        {
            return MySqlExtensions.RunSqlQuery<CursorMarker>(@"SELECT meas.pk_measurement, curs.cursor_elem, curs.scan_pos, curs.value
FROM tbl_bio_biometry_cursors curs
JOIN tbl_bio_measurement meas ON curs.fk_measurement = meas.pk_measurement
WHERE meas.fk_examid = @ExamId AND meas.eye = @Eye
ORDER BY meas.pk_measurement, curs.cursor_elem;", (reader, observer) => observer.OnNext(new CursorMarker(reader.GetInt32(0), (CursorElement)reader.GetByte(1), reader.GetFloat(2), reader.GetFloat(3))), AddSqlParameters(exam), scheduler);
        }
        private static IObservable<SegmentLength> ReadDimensions(LenstarExam exam, IScheduler scheduler)
        {
            return MySqlExtensions.RunSqlQuery<SegmentLength>(@"SELECT meas.pk_measurement, dimen.element, dimen.dimension, dimen.used
FROM tbl_bio_biometry_dimensions dimen
JOIN tbl_bio_measurement meas ON dimen.fk_measurement = meas.pk_measurement
WHERE meas.fk_examid = @ExamId AND meas.eye = @Eye
ORDER BY meas.pk_measurement, dimen.element;", (reader, observer) => observer.OnNext(new SegmentLength(reader.GetInt32(0), (Dimension)reader.GetByte(1), reader.GetFloat(2), reader.GetBoolean(3))), AddSqlParameters(exam), scheduler);
        }
        private static IObservable<BiometryData> ReadBiometryData(LenstarExam exam, IScheduler scheduler)
        {
            return MySqlExtensions.RunSqlQuery<BiometryData>(@"SELECT meas.pk_measurement, biom.algorithm
FROM tbl_bio_measurement meas
JOIN tbl_bio_biometry biom ON meas.pk_measurement = biom.fk_measurement
WHERE meas.fk_examid = @ExamId AND meas.eye = @Eye
ORDER BY meas.pk_measurement;", (reader, observer) => observer.OnNext(new BiometryData(reader.GetInt32(0), (Algorithm)reader.GetByte(1))), AddSqlParameters(exam), scheduler);
        }

        private static float[] SumDecompressedScans(float[] spike1, float[] spike2)
        {
            return Enumerable.Range(0, Math.Min(spike1.Length, spike2.Length)).Select(i => spike1[i] + spike2[i]).ToArray();
        }
        private static SpikeData TransformSpikesForRender(float[] spikes, int count)
        {
            double maxValue = 0;

            var answer = new double[spikes.Length];

            for (var i = 0; i < spikes.Length; i++)
            {
                answer[i] = Math.Pow(Math.Max(0, Math.Log(spikes[i] / count, 2)), spikesPower);
                maxValue = Math.Max(maxValue, answer[i]);
            }

            return new SpikeData(answer, maxValue);
        }

        private record CompressedSpikes(int MeasurementId, byte Index, int ScanLength, byte[] CompressedScan)
        {
            public Task<DecompressedSpikes> DecompressAsync(CancellationToken token)
            {
                using var ms = new MemoryStream(CompressedScan);
                using var zip = new InflaterInputStream(ms);
                using var br = new BinaryReader(zip);

                var decompressed = new float[ScanLength];
                var buff = new byte[4];

                for (var i = 0; i < ScanLength; i++)
                {
                    token.ThrowIfCancellationRequested();
                    decompressed[i] = BinaryPrimitives.ReadSingleBigEndian(br.ReadBytes(4));
                }

                return Task.FromResult(new DecompressedSpikes(MeasurementId, Index, ScanLength, decompressed));
            }
        }
        private record DecompressedSpikes(int MeasurementId, byte Index, int ScanLength, float[] DecompressedScan);
        private record CursorMarker(int MeasurementId, CursorElement Cursor, float ScanPos, float Value);
        private record SegmentLength(int MeasurementId, Dimension Element, float Dimension, bool Used);
        private record BiometryData(int MeasurementId, Algorithm Algorithm);
        private record SpikeData(double[] Spikes, double MaxValue);
        private enum Algorithm : byte
        {
            Standard = 0,
            Composite = 1
        }

        public override string? UrlPathSegment { get; }
        public override string Title { get; }
    }
}
