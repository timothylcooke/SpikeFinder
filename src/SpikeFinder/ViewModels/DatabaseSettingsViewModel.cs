using Microsoft.Win32;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SpikeFinder.Settings;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;

namespace SpikeFinder.ViewModels
{
    public class DatabaseSettingsViewModel : SfViewModel
    {
        public DatabaseSettingsViewModel()
        {
            SqliteDatabasePath = SfMachineSettings.Instance.SqliteDatabasePath ?? SfSettings.DefaultSqlitePath;

            string? connStr = null;
            try
            {
                connStr = SfMachineSettings.Instance.ConnectionString?.Unprotect();
            }
            catch { }

            ConnectionString = connStr ?? "Server=127.0.0.1;Port=3307;Database=octosoft;Uid=root;Pwd=";
            HideConnectionString = connStr is not null;

            this.WhenActivated(d =>
            {
                d(BrowseSqliteDatabasePathCommand = ReactiveCommand.Create<Window, string?>(BrowsePath));

                d(BrowseSqliteDatabasePathCommand.WhereNotNull().BindTo(this, x => x.SqliteDatabasePath));

                d(this.WhenAnyValue(x => x.HideConnectionString).Select(x => x ? Visibility.Collapsed : Visibility.Visible).BindTo(this, x => x.ConnectionStringVisibility));

                d(SaveCommand = ReactiveCommand.Create(SaveSettings));

                if (HostScreen.Router.NavigationStack.Count == 1)
                {
                    d(SaveCommand.Select(_ => (IRoutableViewModel)new LoadGridViewModel()).InvokeCommand(HostScreen.Router.NavigateAndReset));
                }
                else
                {
                    d(SaveCommand.InvokeCommand(HostScreen.Router.NavigateBack));
                }
            });
        }

        private string? BrowsePath(Window window)
        {
            var dlg = new OpenFileDialog() { DefaultExt = "sqlite3db", CheckFileExists = false, AddExtension = true, Title = "SQLite Database Path", Filter = "SQLite Database files (*.sqlite3db)|*.sqlite3db|All files|*.*" };

            if (!string.IsNullOrEmpty(SqliteDatabasePath))
            {
                dlg.FileName = SqliteDatabasePath;
                if (!File.Exists(SqliteDatabasePath))
                {
                    var dir = Path.GetDirectoryName(SqliteDatabasePath);

                    if (Directory.Exists(dir))
                    {
                        dlg.InitialDirectory = dir;
                    }
                }
            }

            if (dlg.ShowDialog(window) == true)
            {
                return dlg.FileName switch { "" => null, _ => dlg.FileName };
            }

            return null;
        }
        private void SaveSettings()
        {
            var settings = SfMachineSettings.Instance;
            settings.SqliteDatabasePath = SqliteDatabasePath;
            settings.ConnectionString = new ProtectedString(ConnectionString);
        }

        [Reactive] public string SqliteDatabasePath { get; set; }
        [Reactive] public string ConnectionString { get; set; }
        [Reactive] public bool HideConnectionString { get; set; }
        [Reactive] public Visibility ConnectionStringVisibility { get; private set; }

        [Reactive] public ReactiveCommand<Window, string?>? BrowseSqliteDatabasePathCommand { get; private set; }

        public override string? Title => "Settings";
        public override string UrlPathSegment => "/Settings";
    }
}
