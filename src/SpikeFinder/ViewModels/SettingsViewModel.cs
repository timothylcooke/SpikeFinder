using Microsoft.Win32;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SpikeFinder.Attributes;
using SpikeFinder.Extensions;
using SpikeFinder.RefractiveIndices;
using SpikeFinder.Settings;
using SpikeFinder.SQLite;
using SpikeFinder.Toast;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SpikeFinder.ViewModels
{
    public class SettingsViewModel : SfViewModel
    {
        public SettingsViewModel()
        {
            SettingsPages = EnumExtensions.GetEnumDescriptions<SettingsPage>().Select(x => new { Page = x.Key, Icon = x.Key.GetCustomEnumAttributes<IconAttribute>().Single().PathData, Description = x.Value }).ToArray();
            RefractiveIndexMethods = EnumExtensions.GetEnumDescriptions<RefractiveIndexMethods>().Select(x => new { Method = x.Key, Description = x.Value }).ToArray();
            CurrentPage = SettingsPage.Database;

            SqliteDatabasePath = SfMachineSettings.Instance.SqliteDatabasePath ?? SfSettings.DefaultSqlitePath;

            RefractiveIndexMethod = SfMachineSettings.Instance.RefractiveIndexMethod;
            PseudophakicDefaultRefractiveIndex = SfMachineSettings.Instance.PseudophakicDefaultRefractiveIndex;
            PseudophakicAcrylicRefractiveIndex = SfMachineSettings.Instance.PseudophakicAcrylicRefractiveIndex;
            PseudophakicPMMARefractiveIndex = SfMachineSettings.Instance.PseudophakicPMMARefractiveIndex;
            PseudophakicSiliconeRefractiveIndex = SfMachineSettings.Instance.PseudophakicSiliconeRefractiveIndex;
            RetinaRefractiveIndex = SfMachineSettings.Instance.RetinaRefractiveIndex;
            SiliconeOilRefractiveIndex = SfMachineSettings.Instance.SiliconeOilRefractiveIndex;

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

                FetchConnectionStringCommand = ReactiveCommand.Create<Unit, Unit>(x => x);
                d(FetchConnectionStringCommand.SelectMany(_ => MySqlExtensions.ReadConnectionStringFromEyeSuite())
                    .Do(x =>
                    {
                        if (x is null)
                        {
                            App.SpikeFinderMainWindow.Notify(Severity.Information, "Failed to load the connection string from EyeSuite.", dismissAfter: TimeSpan.FromSeconds(5));
                        }
                    })
                    .WhereNotNull()
                    .BindTo(this, x => x.ConnectionString));

                d(BrowseCommand = ReactiveCommand.Create<TextBox, (TextBox textBox, string? path)>(x => (x, BrowsePath(x))));
                d(BrowseCommand.Where(x => x.path is not null).Subscribe(x => x.textBox.Text = x.path));

                d(MergeCommand = ReactiveCommand.CreateFromObservable(() => Observable.FromAsync(async ct => await MergeDatabases(ct, MainDatabasePath, AdditionalDatabasePath)).Catch((Exception ex) =>
                {
                    App.SpikeFinderMainWindow.NotifyException(ex);
                    return Observable.Return<string?>(null);
                }), this.WhenAnyValue(x => x.MainDatabasePath, x => x.AdditionalDatabasePath, (main, additional) => !string.IsNullOrWhiteSpace(main) && !string.IsNullOrWhiteSpace(additional) && File.Exists(main) && File.Exists(additional))));
                d(MergeCommand.WhereNotNull().Subscribe(x => App.SpikeFinderMainWindow.Notify(Severity.Success, x, "Successfully Merged Databases")));

                d(this.WhenAnyValue(x => x.HideConnectionString).Select(x => x ? Visibility.Collapsed : Visibility.Visible).BindTo(this, x => x.ConnectionStringVisibility));

                d(SaveCommand = ReactiveCommand.Create(SaveSettings));

                var whenCurrentPage = this.WhenAnyValue(x => x.CurrentPage).Publish();
                d(whenCurrentPage.Skip(1).Zip(whenCurrentPage, (currentPage, previousPage) => (currentPage, previousPage)).Where(x => x.currentPage is null && x.previousPage is not null).Select(x => x.previousPage).ObserveOnDispatcher(DispatcherPriority.Render).BindTo(this, x => x.CurrentPage));

                d(whenCurrentPage.Connect());

                if (HostScreen.Router.NavigationStack.Count == 1)
                {
                    d(SaveCommand.Select(_ => new LoadGridViewModel()).Cast<IRoutableViewModel>().InvokeCommand(HostScreen.Router.NavigateAndReset));
                }
                else
                {
                    d(SaveCommand.InvokeCommand(HostScreen.Router.NavigateBack));
                }
            });
        }

        private string? BrowsePath(Window window)
        {
            return BrowsePath(window, SqliteDatabasePath);
        }
        private string? BrowsePath(TextBox textBox)
        {
            return BrowsePath(Window.GetWindow(textBox), textBox.Text);
        }
        private string? BrowsePath(Window window, string defaultPath)
        {
            var dlg = new OpenFileDialog() { DefaultExt = "sqlite3db", CheckFileExists = true, AddExtension = true, Title = "SQLite Database Path", Filter = "SQLite Database files (*.sqlite3db)|*.sqlite3db|All files|*.*" };

            if (!string.IsNullOrEmpty(defaultPath))
            {
                dlg.FileName = defaultPath;
                if (!File.Exists(defaultPath))
                {
                    var dir = Path.GetDirectoryName(defaultPath);

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

        private async Task<string> MergeDatabases(CancellationToken token, string? main, string? additional)
        {
            if (string.IsNullOrWhiteSpace(main) || !File.Exists(main))
                throw new Exception("The main database file does not exist.");
            if (string.IsNullOrWhiteSpace(additional) || !File.Exists(additional))
                throw new Exception("The additional database file does not exist.");

            SQLiteDatabase mainDb, additionalDb;

            try
            {
                mainDb = await SQLiteDatabase.OpenOrCreateDatabase(main!, token);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to open the main database file", ex);
            }
            try
            {
                additionalDb = await SQLiteDatabase.OpenOrCreateDatabase(additional!, token);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to open the main database file", ex);
            }

            int total = 0;
            int success = 0;

            using (mainDb)
            {
                using (additionalDb)
                {
                    await foreach (var spike in additionalDb.LoadPersistedSpikes(token))
                    {
                        if (await mainDb.SaveSpikes(spike.key, spike.spikes, false, token))
                            success++;
                        total++;
                    }
                }
            }

            return $"Spikes from {success} of {total} eye{(total == 1 ? "" : "s")} were added to the main database.{(success == total ? "" : $" Spikes from the remaining {total - success} eye{(total - success == 1 ? "" : "s")} were ignored because spikes for {(total - success == 1 ? "that eye" : "those eyes")} already existed in the main database.")}";
        }

        private void SaveSettings()
        {
            var settings = SfMachineSettings.Instance;
            settings.SqliteDatabasePath = SqliteDatabasePath;
            settings.ConnectionString = new ProtectedString(ConnectionString);
            settings.RefractiveIndexMethod = RefractiveIndexMethod;
            settings.PseudophakicDefaultRefractiveIndex = PseudophakicDefaultRefractiveIndex;
            settings.PseudophakicAcrylicRefractiveIndex = PseudophakicAcrylicRefractiveIndex;
            settings.PseudophakicPMMARefractiveIndex = PseudophakicPMMARefractiveIndex;
            settings.PseudophakicSiliconeRefractiveIndex = PseudophakicSiliconeRefractiveIndex;
            settings.RetinaRefractiveIndex = RetinaRefractiveIndex;
            settings.SiliconeOilRefractiveIndex = SiliconeOilRefractiveIndex;
        }

        [Reactive] public string SqliteDatabasePath { get; set; }
        [Reactive] public string ConnectionString { get; set; }
        [Reactive] public bool HideConnectionString { get; set; }
        [Reactive] public Visibility ConnectionStringVisibility { get; private set; }

        [Reactive] public ReactiveCommand<Unit, Unit> FetchConnectionStringCommand { get; private set; }
        [Reactive] public ReactiveCommand<Window, string?>? BrowseSqliteDatabasePathCommand { get; private set; }
        [Reactive] public ReactiveCommand<TextBox, (TextBox textBox, string? path)>? BrowseCommand { get; private set; }
        [Reactive] public ReactiveCommand<Unit, string?>? MergeCommand { get; private set; }

        [Reactive] public string? MainDatabasePath { get; set; }
        [Reactive] public string? AdditionalDatabasePath { get; set; }

        [Reactive] public SettingsPage? CurrentPage { get; set; }
        public object[] SettingsPages { get; }


        [Reactive] public RefractiveIndexMethods RefractiveIndexMethod { get; set; }
        [Reactive] public string PseudophakicDefaultRefractiveIndex { get; set; }
        [Reactive] public string PseudophakicAcrylicRefractiveIndex { get; set; }
        [Reactive] public string PseudophakicPMMARefractiveIndex { get; set; }
        [Reactive] public string PseudophakicSiliconeRefractiveIndex { get; set; }
        [Reactive] public string RetinaRefractiveIndex { get; set; }
        [Reactive] public string SiliconeOilRefractiveIndex { get; set; }
        public object[] RefractiveIndexMethods { get; }


        public enum SettingsPage
        {
            [Icon("M24 22q-8.05 0-13.025-2.45T6 14q0-3.15 4.975-5.575Q15.95 6 24 6t13.025 2.425Q42 10.85 42 14q0 3.1-4.975 5.55Q32.05 22 24 22Zm0 10q-7.3 0-12.65-2.2Q6 27.6 6 24.5v-5q0 1.95 1.875 3.375t4.65 2.35q2.775.925 5.9 1.35Q21.55 27 24 27q2.5 0 5.6-.425 3.1-.425 5.875-1.325 2.775-.9 4.65-2.325Q42 21.5 42 19.5v5q0 3.1-5.35 5.3Q31.3 32 24 32Zm0 10q-7.3 0-12.65-2.2Q6 37.6 6 34.5v-5q0 1.95 1.875 3.375t4.65 2.35q2.775.925 5.9 1.35Q21.55 37 24 37q2.5 0 5.6-.425 3.1-.425 5.875-1.325 2.775-.9 4.65-2.325Q42 31.5 42 29.5v5q0 3.1-5.35 5.3Q31.3 42 24 42Z")]
            Database,
            [Description("Refractive Indices"), Icon("M13.8 44q-1.65 0-2.625-.95-.975-.95-1.125-2.45L6 4h36l-4.05 36.6q-.15 1.5-1.125 2.45-.975.95-2.575.95Zm-3.1-25 2.4 22h21.8l2.4-22Zm-.35-3h27.3l1-9H9.35ZM37.3 19H10.7h26.6ZM22.042 16h3.916l-7.552-9h-3.916zm-10.069-12h3.916l-3.356-4h-3.916zm11.592 15h3.046l3.879 22h-3.046z")]
            RefractiveIndices,
            [Description("Merge Databases"), Icon("M960,960m-704,-120l-56,-56l193,-194q23,-23,35,-52t12,-61v-204l-64,63l-56,-56l160,-160l160,160l-56,56l-64,-63v204q0,32,12,61t35,52l193,194l-56,56l-224,-224l-224,224z")]
            MergeDatabases,
        }


        public override string? Title => "Settings";
        public override string UrlPathSegment => "/Settings";
    }
}
