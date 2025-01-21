using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using MySqlConnector;
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
        public static IObservable<T> Connect<T>(Func<MySqlCommand, IObserver<T>, Task> runQuery)
        {
            return Observable.Create<T>(async (obs, token) =>
            {
                await using var mysql = new MySqlConnection(SfMachineSettings.Instance.ConnectionString!.Unprotect());
                await mysql.OpenAsync(token);

                await using var cmd = mysql.CreateCommand();
                cmd.CommandTimeout = 2147483;
                cmd.CommandText = "set net_write_timeout=99999;set net_read_timeout=99999;";
                await cmd.ExecuteNonQueryAsync(token);

                await runQuery(cmd, obs);

                obs.OnCompleted();
            });
        }

        [Obsolete]
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

            if (reader is null)
                throw new TaskCanceledException();

            await readResults(reader);

            return accumulateResult();
        }, token);

        [Obsolete]
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

        private static IObservable<MemoryStream?> ReadDbx()
        {
            return Observable.StartAsync(async ct =>
            {
                try
                {
                    var eyeSuiteDbx = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Haag-Streit", "EyeSuite", "eyesuite.dbx");

                    if (!File.Exists(eyeSuiteDbx))
                        return null;

                    using var fs = File.Open(eyeSuiteDbx, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var ms = new MemoryStream();

                    var buff = new byte[fs.Length];

                    int br;
                    do
                    {
                        br = await fs.ReadAsync(buff, 0, buff.Length, ct);
                        await ms.WriteAsync(buff, 0, br, ct);
                    } while (br > 0);

                    ms.Position = 0;

                    return ms;
                }
                catch
                {
                    return null;
                }
            });
        }

        private static async Task<T?> ReadStream<T>(Stream? str, Func<Task<T>> action)
        {
            if (str is null)
                return default;

            var originalPosition = str.Position;

            try
            {
                return await action();
            }
            catch
            {
                return default;
            }
            finally
            {
                str.Position = originalPosition;
            }
        }
        private static async Task<byte[]?> ReadBytes(Stream? str, long bytes, CancellationToken ct)
        {
            if (str!.Length - str.Position < bytes)
                return default;

            var buff = new byte[bytes];

            await str.ReadExactlyAsync(buff, 0, buff.Length, ct);

            return buff;
        }
        private static Task<string?> DecryptDES(Stream? str, CancellationToken ct) =>
            ReadStream(str, async () =>
            {
                using var des = DES.Create();

                des.Key = BitConverter.GetBytes(0x76BA4C239D4F73E9);
                des.Mode = CipherMode.CFB;
                des.Padding = PaddingMode.None;
                des.IV = new byte[8];
                des.FeedbackSize = 8;

                using var decryptor = des.CreateDecryptor();

                var cs = new CryptoStream(str!, decryptor, CryptoStreamMode.Read);
                var sr = new StreamReader(cs, Encoding.UTF8);

                return await sr.ReadToEndAsync(ct);
            });
        private static Task<string?> DecryptAES(Stream? str, CancellationToken ct) =>
            ReadStream(str, async () =>
            {
                const int tagSize = 16;

                if (await ReadBytes(str, 16, ct) is not { } salt || await ReadBytes(str, 12, ct) is not { } nonce || await ReadBytes(str, str!.Length - str.Position - tagSize, ct) is not { } cipher || await ReadBytes(str, tagSize, ct) is not { } tag)
                    return default;

                var key = KeyDerivation.Pbkdf2(Encoding.UTF8.GetString(Guid.Parse("42652554-345b-537a-6124-723635216557").ToByteArray()), salt, KeyDerivationPrf.HMACSHA512, 65535, 32);

                var aes = new AesGcm(key, tagSize);

                var plain = new byte[cipher.Length];
                aes.Decrypt(nonce, cipher, tag, plain);

                return Encoding.UTF8.GetString(plain);
            });

        public static IObservable<string?> ReadConnectionStringFromEyeSuite()
        {
            return ReadDbx()
                .SelectMany(async (dbx, ct) => await DecryptAES(dbx, ct) ?? await DecryptDES(dbx, ct))
                .Select(x => x?
                        .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries)
                        .Where(x => !x.StartsWith("#") && x.Contains('='))
                        .GroupBy(x => x.Substring(0, x.IndexOf('=')))
                        .ToDictionary(x => x.Key, x => x.First().Substring(x.Key.Length + 1)))
                .Select(x => x is null ? null : new Func<string, string?>(key =>
                {
                    if (x?.TryGetValue(key, out var value) is true)
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
                }))
                .Select(GetIniValue =>
                {
                    if (GetIniValue is null)
                        return null;

                    var isLocal = GetIniValue("remoteConnection") != "true";

                    return new MySqlConnectionStringBuilder()
                    {
                        Port = isLocal ? 3307 : uint.Parse(GetIniValue("port") ?? "3307"),
                        Server = isLocal ? "localhost" : GetIniValue("location") ?? "localhost",
                        Database = GetIniValue("name") ?? "octosoft",
                        UserID = isLocal ? "hsuser" : GetIniValue("username") ?? "root",
                        Password = isLocal ? "J,mFP%5m7Tkp7Vdc" : (GetIniValue("password") ?? "")
                    }.ConnectionString;
                })
                .Catch((Exception ex) =>
                {
                    App.SpikeFinderMainWindow.NotifyException(ex);
                    return Observable.Return<string?>(null);
                });
        }
    }
}
