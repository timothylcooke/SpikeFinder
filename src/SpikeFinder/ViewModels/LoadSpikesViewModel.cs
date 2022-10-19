#nullable enable

using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using MySql.Data.MySqlClient;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SpikeFinder.Extensions;
using SpikeFinder.Models;
using SpikeFinder.Toast;
using Syncfusion.Data.Extensions;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SpikeFinder.ViewModels
{
    public class LoadSpikesViewModel : SfViewModel
    {
        const int imageHeight = 1200;
        const int imageWidth = 4800;
        const double spikesPower = 1.8;

        public LoadSpikesViewModel(LenstarExam exam)
        {
            UrlPathSegment = $"/LoadSpikes/{exam.Key}";
            Title = $"Loading {exam.FirstName} {exam.LastName} (DOB {exam.DOB:d}; #{exam.PatientNumber}) {exam.Eye} measurement {exam.Timestamp:d}…";

            var loadingItemCount = new LoadingItemViewModel(1, "Figuring out much stuff there is to load…");
            var loadingCursors = new LoadingItemViewModel(2, "Loading Cursor positions…");
            var loadingSpikeData = new LoadingItemViewModel(3, "Loading Spike Data…");
            var renderingSpikes = new LoadingItemViewModel(4, "Rendering Spikes…");
            LoadingItems = new[] { loadingItemCount, loadingSpikeData, loadingCursors, renderingSpikes };

            this.WhenActivated(d =>
            {
                d(CountTotalScans(exam, RxApp.TaskpoolScheduler)
                    .ToPropertyEx(this, x => x.MeasurementsToRead));

                // reading and decompressing are done once per ascan.
                new[] { loadingItemCount, loadingSpikeData }.ForEach(x => d(x.Initialize(this.WhenAnyValue(y => y.MeasurementsToRead).Select(y => y.TotalScans))));

                // There are six cursors, six dimensions, and one biometry per measurement. So 13 items to read.
                d(loadingCursors.Initialize(exam.PersistedSpikes == null ? this.WhenAnyValue(y => y.MeasurementsToRead).Select(y => y.Measurements * 13) : Observable.Return<long?>(0)));

                // There is one spike per measurement, plus a merged spike we must render. Each render job is split into {Environment.ProcessorCount} pieces.
                d(renderingSpikes.Initialize(this.WhenAnyValue(y => y.MeasurementsToRead).Select(y => (y.Measurements + 1) * (long?)Environment.ProcessorCount)));

                // Mark loadingItemCount as finished as soon as we load the total ascan data count.
                d(this.WhenAnyValue(y => y.MeasurementsToRead).Where(x => x.TotalScans.HasValue).Take(1).ObserveOn(RxApp.MainThreadScheduler).Subscribe(x => loadingItemCount.SlowlyUpdatingProgress = x.TotalScans!.Value));

                var readAndDecompressSpikes =
                    // Fetch the spike data from MySQL
                    ReadSpikes(exam, RxApp.TaskpoolScheduler)
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

                var renderedSpikes = mergeSpikes
                    .SelectMany(x => Enumerable.Range(0, Environment.ProcessorCount).Select(y => (x.MeasurementId, StripNumber: y, x.Spikes)))
                    .Select(x => Observable.DeferAsync(async token => Observable.Return(await Task.Run(() => RenderImagePortion(x.MeasurementId, x.StripNumber, x.Spikes), token), RxApp.TaskpoolScheduler)))
                    .Merge(Environment.ProcessorCount)
                    .ObserveOnDispatcher()
                    .Do(_ => renderingSpikes.SlowlyUpdatingProgress++)
                    .ObserveOn(RxApp.TaskpoolScheduler)
                    .GroupBy(x => x.MeasurementId)
                    .SelectMany(x => Observable.Return(x.Key).CombineLatest(x.ToList(), (measurementId, images) => (measurementId, images)))
                    .ToDictionary(x => x.measurementId, x => (x.images[0].Spikes, Image: RenderImage(x.images.Select(y => (y.StripNumber, y.Image)))));

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
                        .ObserveOnDispatcher()
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

                d(renderedSpikes
                    .CombineLatest(cursors, (renderedSpikes, cursors) => (renderedSpikes, cursors))
                    .Select(x => new SpikesViewModel(exam, x.renderedSpikes[-1].Spikes.Spikes, x.renderedSpikes[-1].Spikes.MaxValue, MergeImages(x.renderedSpikes.Select(x => x.Value.Image)), x.cursors))
                    .Cast<IRoutableViewModel>()
                    .ObserveOnDispatcher()
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

        private byte[] MergeImages(IEnumerable<byte[]> images)
        {
            var visual = new DrawingVisual();
            var context = visual.RenderOpen();

            foreach (var image in images)
            {
                using (var ms = new MemoryStream(image))
                {
                    var stripBmp = new BitmapImage();
                    stripBmp.BeginInit();
                    stripBmp.StreamSource = ms;
                    stripBmp.CacheOption = BitmapCacheOption.OnLoad;
                    stripBmp.EndInit();

                    context.DrawImage(stripBmp, new Rect(0, 0, imageWidth, imageHeight));
                }
            }
            context.Close();

            return RenderVisualToPng(visual, imageWidth, imageHeight);
        }

        [ObservableAsProperty] private (long? Measurements, long? TotalScans) MeasurementsToRead { get; }
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

        private static byte[] RenderVisualToPng(Visual visual, int width, int height)
        {
            var bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);

            using var ms = new MemoryStream();
            var encoder = new PngBitmapEncoder();

            encoder.Frames.Add(BitmapFrame.Create(bmp));
            encoder.Save(ms);

            return ms.ToArray();
        }
        private static (int MeasurementId, int StripNumber, byte[] Image, SpikeData Spikes) RenderImagePortion(int measurementId, int stripNumber, SpikeData spikes)
        {
            var visual = new DrawingVisual();
            var context = visual.RenderOpen();

            var pointsPerStrip = (int)Math.Ceiling((double)spikes.Spikes.Length / Environment.ProcessorCount);

            var firstPoint = pointsPerStrip * stripNumber;
            var numPoints = Math.Min(firstPoint + pointsPerStrip, spikes.Spikes.Length - 1) - firstPoint;

            var stripWidth = (double)imageWidth / Environment.ProcessorCount;

            // bounds represents the x-y position of coordinates (0,0) - (1,1).
            // x coordinate is the index in the array; y coordinate is the value at that index.
            var bounds = new Rect(-stripNumber * stripWidth, imageHeight, stripWidth / pointsPerStrip, imageHeight / spikes.MaxValue);

            Point CoordinatesOf(int i) => new(bounds.X + bounds.Width * i, bounds.Y - bounds.Height * spikes.Spikes[i]);

            var brush = measurementId < 0 ? Brushes.Black : (measurementId % 2) switch { 0 => Brushes.Red, _ => Brushes.Green };
            //var brush = measurementId < 0 ? Brushes.Black : (measurementId % 6) switch { 0 => Brushes.Red, 1 => Brushes.Orange, 2 => Brushes.LightGreen, 3 => Brushes.DarkGreen, 4 => Brushes.Blue, _ => Brushes.Violet };

            var tallest = Enumerable.Range(firstPoint + 1, numPoints).Select(i => CoordinatesOf(i)).Max(x => x.Y);


            var geometry = new PathGeometry(new[] { new PathFigure(new Point(0, 0), Enumerable.Range(firstPoint, numPoints).Select(i => new LineSegment(CoordinatesOf(i), i > firstPoint)), false) });
            var drawing = new GeometryDrawing(null, new Pen(brush, 1), geometry);
            var image = new DrawingImage(drawing);

            var drawingBounds = drawing.Bounds;

            context.DrawImage(image, new Rect(drawingBounds.X, drawingBounds.Y, stripWidth - drawingBounds.X, drawingBounds.Height));
            context.Close();

            return (measurementId, stripNumber, RenderVisualToPng(visual, (int)Math.Round(stripWidth), imageHeight), spikes);
        }
        private static byte[] RenderImage(IEnumerable<(int StripNumber, byte[] Image)> imagePortions)
        {
            var visual = new DrawingVisual();
            var context = visual.RenderOpen();

            var stripWidth = imageWidth / Environment.ProcessorCount;

            foreach (var image in imagePortions)
            {
                using (var ms = new MemoryStream(image.Image))
                {
                    var stripBmp = new BitmapImage();
                    stripBmp.BeginInit();
                    stripBmp.StreamSource = ms;
                    stripBmp.CacheOption = BitmapCacheOption.OnLoad;
                    stripBmp.EndInit();

                    context.DrawImage(stripBmp, new Rect(image.StripNumber * stripWidth, 0, stripWidth, imageHeight));
                }
            }
            context.Close();

            return RenderVisualToPng(visual, imageWidth, imageHeight);
        }

        private static Action<MySqlCommand> AddSqlParameters(LenstarExam exam) => cmd =>
        {
            cmd.Parameters.AddWithValue("@ExamId", exam.ExamId);
            cmd.Parameters.AddWithValue("@Eye", (byte)exam.Eye);
        };

        private static IObservable<(long? Measurements, long? TotalScans)> CountTotalScans(LenstarExam exam, IScheduler scheduler)
        {
            return MySqlExtensions.RunSqlQuery<(long?, long?)>(@"SELECT COUNT(*) TotalMeasurements, SUM(n) TotalAscans FROM (
    SELECT meas.pk_measurement, COUNT(*) n
    FROM tbl_bio_ascan ascan
    JOIN tbl_bio_measurement meas ON ascan.fk_measurement = meas.pk_measurement
    WHERE meas.fk_examid = @ExamId AND meas.eye = @Eye AND ascan.used = 1
    GROUP BY meas.pk_measurement
) measurements;", (reader, observer) => observer.OnNext((reader.GetInt64(0), reader.GetInt64(1))), AddSqlParameters(exam), scheduler);
        }
        private static IObservable<CompressedSpikes> ReadSpikes(LenstarExam exam, IScheduler scheduler)
        {
            return MySqlExtensions.RunSqlQuery<CompressedSpikes>(@"SELECT ascan.fk_measurement, ascan.idx, ascan.scan_length, ascan.scandata
FROM tbl_bio_ascan ascan
JOIN tbl_bio_measurement meas ON ascan.fk_measurement = meas.pk_measurement
WHERE meas.fk_examid = @ExamId AND meas.eye = @Eye AND ascan.used = 1
ORDER BY ascan.fk_measurement, ascan.idx;", (reader, observer) => observer.OnNext(new CompressedSpikes(reader.GetInt32(0), reader.GetByte(1), reader.GetInt32(2), (byte[])reader[3])), AddSqlParameters(exam), scheduler);
        }
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
