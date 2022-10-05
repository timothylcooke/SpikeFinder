using MySql.Data.MySqlClient;
using SpikeFinder.Settings;
using System;
using System.Data.Common;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SpikeFinder.Extensions
{
    public static class MySqlExtensions
    {
        public static Task<T> RunSqlQuery<T>(string query, Func<DbDataReader, Task> readResults, Func<T> accumulateResult, CancellationToken token, Action<MySqlCommand>? addParameters = null) => Task.Run(async () =>
        {
            await using var mysql = new MySqlConnection(SfMachineSettings.Instance.ConnectionString!.Unprotect());
            await mysql.OpenAsync(token);

            await using var cmd = mysql.CreateCommand();

            cmd.CommandTimeout = 2147483;
            cmd.CommandText = "set net_write_timeout=99999;set net_read_timeout=99999;";
            await cmd.ExecuteNonQueryAsync(token);

            cmd.CommandText = query;
            addParameters?.Invoke(cmd);

            await using var reader = await cmd.ExecuteReaderAsync(token);
            await readResults(reader);

            return accumulateResult();
        }, token);
        public static IObservable<T> RunSqlQuery<T>(string query, Action<DbDataReader, IObserver<T>> readNext, Action<MySqlCommand> addSqlParameters, IScheduler scheduler) => Observable.Create<T>(async (observer, token) =>
        {
            try
            {
                await RunSqlQuery(query, async reader =>
                {
                    while (await reader.ReadAsync(token))
                    {
                        readNext(reader, observer);
                    }
                    observer.OnCompleted();
                }, () => Unit.Default, token, addSqlParameters);
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
                observer.OnCompleted();
            }
        }).SubscribeOn(scheduler);
    }
}
