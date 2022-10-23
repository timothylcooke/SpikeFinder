using SpikeFinder.Models;
using SpikeFinder.Settings;
using SpikeFinder.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace SpikeFinder.SQLite
{
    public class SQLiteDatabase : IDisposable
    {
        public static IObservable<(string examKey, PersistedSpikes spikes)> SpikesSaved => _spikesSaved;
        private static Subject<(string, PersistedSpikes)> _spikesSaved = new();

        public static async Task SaveSpikes(string examKey, PersistedSpikes spikes, CancellationToken token)
        {
            using var db = await OpenOrCreateDatabase(token);

            await db.ExecuteTransaction(async () =>
            {
                using var cmd = db.CreateCommand();

                cmd.CommandText = @"INSERT OR REPLACE INTO Spikes VALUES(@ExamKey, @PosteriorCornea, @AnteriorLens, @PosteriorLens, @ILM, @RPE, @Notes, @MeasureMode);";
                cmd.Parameters.AddWithValue("ExamKey", examKey);
                cmd.Parameters.AddWithValue("PosteriorCornea", spikes.PosteriorCornea);
                cmd.Parameters.AddWithValue("AnteriorLens", spikes.AnteriorLens);
                cmd.Parameters.AddWithValue("PosteriorLens", spikes.PosteriorLens);
                cmd.Parameters.AddWithValue("ILM", spikes.ILM);
                cmd.Parameters.AddWithValue("RPE", spikes.RPE);
                cmd.Parameters.AddWithValue("Notes", spikes.Notes);
                cmd.Parameters.AddWithValue("MeasureMode", (object?)spikes.MeasureMode ?? DBNull.Value);

                if (await cmd.ExecuteNonQueryAsync() != 1)
                    throw new Exception("Failed to save the spikes.");
            }, token);

            _spikesSaved.OnNext((examKey, spikes));
        }
        public static async Task<Dictionary<string, PersistedSpikes>> LoadPersistedSpikes(LoadingItemViewModel loadingProgress, Action<IDisposable> addSubscription, CancellationToken token)
        {
            using var db = await OpenOrCreateDatabase(token);
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Spikes";
            addSubscription(loadingProgress.Initialize(Observable.Return((long?)await cmd.ExecuteScalarAsync(token))));

            var results = new Dictionary<string, PersistedSpikes>();

            cmd.CommandText = "SELECT * FROM Spikes;";

            using var reader = await cmd.ExecuteReaderAsync(token);

            async Task<int?> ReadInt32(int index) => await reader.IsDBNullAsync(index) ? new int?() : reader.GetInt32(index);
            async Task<byte?> ReadByte(int index) => await reader.IsDBNullAsync(index) ? new byte?() : reader.GetByte(index);

            while (await reader.ReadAsync())
            {
                results[reader.GetString(0)] = new(await ReadInt32(1), await ReadInt32(2), await ReadInt32(3), await ReadInt32(4), await ReadInt32(5), reader.GetString(6), (MeasureMode?)await ReadByte(7));
                loadingProgress.ActualProgress++;
            }

            return results;
        }

        private static Task<SQLiteDatabase> OpenOrCreateDatabase(CancellationToken token) => OpenOrCreateDatabase(SfMachineSettings.Instance.SqliteDatabasePath!, token);
        private static async Task<SQLiteDatabase> OpenOrCreateDatabase(string path, CancellationToken token)
        {
            var sql = new SQLiteDatabase(path);

            await sql.OpenAsync(token);

            using (var cmd = sql.CreateCommand())
            {
                await sql.ExecuteTransaction(async () =>
                {
                    cmd.CommandText = @"CREATE TABLE IF NOT EXISTS Version(Version INT);SELECT Version FROM Version;";

                    var version = await cmd.ExecuteScalarAsync(token) switch
                    {
                        null => 0,
                        DBNull => 0,
                        int v => v,
                        object x => throw new Exception($"Version is neither an int nor null: {x?.GetType().FullName}")
                    };

                    switch (version)
                    {
                        case 0:
                            await sql.UpgradeToV1(cmd);
                            version = 1;
                            break;
                        case 1:
                            await sql.UpgradeToV2(cmd);
                            version = 2;
                            break;
                    }

                    const int ExpectedVersion = 2;
                    if (version != ExpectedVersion)
                        throw new Exception($"The database is version {version}. This version of SpikeFinder is only compatible with version {ExpectedVersion}.");
                }, token);
            }

            return sql;
        }
        private async Task UpgradeToV1(SQLiteCommand cmd)
        {
            cmd.CommandText = "DELETE FROM Version;";
            await cmd.ExecuteNonQueryAsync();

            cmd.CommandText = @"
CREATE TABLE Spikes(ExamKey CHAR(40) PRIMARY KEY, PosteriorCornea INT, AnteriorLens INT, PosteriorLens INT, ILM INT, RPE INT, Notes TEXT);
INSERT INTO Version VALUES(1);
";
            if (await cmd.ExecuteNonQueryAsync() != 1)
                throw new Exception("Failed to upgrade database to v1");
        }
        private async Task UpgradeToV2(SQLiteCommand cmd)
        {
            cmd.CommandText = "DELETE FROM Version;";
            await cmd.ExecuteNonQueryAsync();

            cmd.CommandText = @"
ALTER TABLE Spikes ADD MeasureMode INT;
INSERT INTO Version VALUES(2);
";
            if (await cmd.ExecuteNonQueryAsync() < 1)
                throw new Exception("Failed to upgrade database to v2");
        }
        private Task OpenAsync(CancellationToken token) => _sql.OpenAsync(token);
        private SQLiteCommand CreateCommand() => _sql.CreateCommand();
        private async Task ExecuteTransaction(Func<Task> transactedSql, CancellationToken token)
        {
            using var tran = await _sql.BeginTransactionAsync(token);

            await transactedSql();

            await tran.CommitAsync(token);
        }

        private SQLiteDatabase(string path)
        {
            if (!File.Exists(path))
            {
                SQLiteConnection.CreateFile(path);
            }

            _sql = new SQLiteConnection($"Data Source={path.Replace(@"\", @"\\")};");
        }

        private SQLiteConnection _sql;

        public void Dispose()
        {
            _sql.Dispose();
        }
    }
}
