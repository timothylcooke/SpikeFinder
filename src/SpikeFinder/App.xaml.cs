#nullable enable

using ControlzEx.Theming;
using ReactiveUI;
using SpikeFinder.Views;
using Splat;
using System;
using System.IO;
using System.Reactive;
using System.Windows;

namespace SpikeFinder
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static AppBootstrapper Bootstrapper { get; private set; }

        static App()
        {
            lock (typeof(App))
            {
                if (Bootstrapper == null)
                {
                    RxApp.DefaultExceptionHandler = Observer.Create<Exception>(ex => SpikeFinderMainWindow.NotifyException(ex));
                    Bootstrapper = new AppBootstrapper();
                }
            }
        }

        public App()
        {
            InitializeComponent();
        }
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            ThemeManager.Current.ChangeTheme(this, "Light.Indigo");
            (MainWindow = SpikeFinderMainWindow).Show();
        }

        public static MainWindow SpikeFinderMainWindow => Locator.Current.GetService<MainWindow>()!;
    }
}
