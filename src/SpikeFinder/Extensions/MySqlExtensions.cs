using MySql.Data.MySqlClient;
using SpikeFinder.Settings;
using System;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Text;
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

        public static string? ReadConnectionStringFromEyeSuite()
        {
            try
            {
                var eyeSuiteDbx = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Haag-Streit", "EyeSuite", "eyesuite.dbx");

                if (File.Exists(eyeSuiteDbx))
                {
                    using var fs = File.Open(eyeSuiteDbx, FileMode.Open, FileAccess.Read, FileShare.Read);

                    using var des = DES.Create();

                    des.Key = new byte[] { 233, 115, 79, 157, 35, 76, 186, 118 };
                    des.Mode = CipherMode.CFB;
                    des.Padding = PaddingMode.None;
                    des.IV = new byte[8];
                    des.FeedbackSize = 8;

                    using var decryptor = des.CreateDecryptor();
                    using var cs = new CryptoStream(fs, decryptor, CryptoStreamMode.Read);
                    using var sr = new StreamReader(cs, Encoding.UTF8);

                    var iniData = sr.ReadToEnd()
                        .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(x => x.Contains('='))
                        .GroupBy(x => x.Substring(0, x.IndexOf('=')))
                        .ToDictionary(x => x.Key, x => x.First().Substring(x.Key.Length + 1));

                    string? GetIniValue(string key)
                    {
                        if (iniData.TryGetValue(key, out var value))
                        {
                            var sb = new StringBuilder();

                            using var sr = new StringReader(value);

                            int nextChar;
                            while (true)
                            {
                                switch (nextChar = sr.Read())
                                {
                                    case -1:
                                        return sb.ToString();
                                    case '\\':
                                        sb.Append((char)sr.Read());
                                        break;
                                    default:
                                        sb.Append((char)nextChar);
                                        break;
                                }
                            }
                        }

                        return null;
                    }

                    return new MySqlConnectionStringBuilder()
                    {
                        Port = uint.Parse(GetIniValue("port") ?? "3307"),
                        Server = GetIniValue("location") ?? "localhost",
                        Database = GetIniValue("name") ?? "octosoft",
                        UserID = GetIniValue("username") ?? "root",
                        Password = GetIniValue("password") ?? ""
                    }.ConnectionString;
                }
            }
            // If anything fails, give up... We tried our best. We'll let the user solve this problem for us.
            catch { }

            return null;
        }
    }
}
