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

        private static async Task<MemoryStream?> ReadDbx()
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
                    br = await fs.ReadAsync(buff, 0, buff.Length);
                    await ms.WriteAsync(buff, 0, br);
                } while (br > 0);

                ms.Position = 0;

                return ms;
            }
            catch
            {
                return null;
            }
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
        private static async Task<byte[]?> ReadBytes(Stream? str, long bytes)
        {
            if (str!.Length - str.Position < bytes)
                return default;

            var buff = new byte[bytes];

            int br = 0;
            do
            {
                br += await str.ReadAsync(buff, br, buff.Length - br);
            } while (br < bytes);

            return buff;
        }
        private static Task<string?> DecryptDES(Stream? str) => ReadStream(str, async () =>
            {
                using var des = DES.Create();

                des.Key = [233, 115, 79, 157, 35, 76, 186, 118];
                des.Mode = CipherMode.CFB;
                des.Padding = PaddingMode.None;
                des.IV = new byte[8];
                des.FeedbackSize = 8;

                using var decryptor = des.CreateDecryptor();

                var cs = new CryptoStream(str!, decryptor, CryptoStreamMode.Read);
                var sr = new StreamReader(cs, Encoding.UTF8);

                return await sr.ReadToEndAsync();
            });
        private static Task<string?> DecryptAES(Stream? str) => ReadStream(str, async () =>
        {
            //using var aes = Aes.Create();

            const int tagSize = 16;

            if (await ReadBytes(str, 16) is not { } salt || await ReadBytes(str, 12) is not { } nonce || await ReadBytes(str, str!.Length - str.Position - tagSize) is not { } ciphertext || await ReadBytes(str, tagSize) is not { } tag)
                return default;

            var key = KeyDerivation.Pbkdf2("T%eB[4zSa$r65!eW", salt, KeyDerivationPrf.HMACSHA512, 65535, 32);

            var aes = new AesGcm(key, tagSize);

            var plaintext = new byte[ciphertext.Length];
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

            return Encoding.UTF8.GetString(plaintext);
        });

        public async static Task<string?> ReadConnectionStringFromEyeSuite()
        {
            try
            {
                using var dbx = await ReadDbx();

                var settings = await DecryptAES(dbx) ?? await DecryptDES(dbx);

                var iniData = settings?
                        .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries)
                        .Where(x => !x.StartsWith("#") && x.Contains('='))
                        .GroupBy(x => x.Substring(0, x.IndexOf('=')))
                        .ToDictionary(x => x.Key, x => x.First().Substring(x.Key.Length + 1));

                string? GetIniValue(string key)
                {
                    if (iniData?.TryGetValue(key, out var value) is true)
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

                if (GetIniValue("remoteConnection") != "true")
                    return new MySqlConnectionStringBuilder() { Port = 3307, Server = "localhost", Database = GetIniValue("name") ?? "octosoft", UserID = "hsuser", Password = "J,mFP%5m7Tkp7Vdc" }.ConnectionString;

                return new MySqlConnectionStringBuilder()
                {
                    Port = uint.Parse(GetIniValue("port") ?? "3307"),
                    Server = GetIniValue("location") ?? "localhost",
                    Database = GetIniValue("name") ?? "octosoft",
                    UserID = GetIniValue("username") ?? "root",
                    Password = GetIniValue("password") ?? ""
                }.ConnectionString;
            }
            // If anything fails, give up... We tried our best. We'll let the user solve this problem for us.
            catch { }

            return null;
        }
    }
}
