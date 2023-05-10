using ControlzEx.Theming;
using ReactiveUI;
using SpikeFinder.Views;
using Splat;
using Syncfusion.Licensing;
using System;
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

                    SyncfusionLicenseProvider.RegisterLicense("Mgo+DSMBaFt+QHJqVk1hXk5Hd0BLVGpAblJ3T2ZQdVt5ZDU7a15RRnVfR1xjSHhXdkBqWX5ecQ==;Mgo+DSMBPh8sVXJ1S0R+X1pFdEBBXHxAd1p/VWJYdVt5flBPcDwsT3RfQF5jTH9Td0NnWXpXcHZQRw==;ORg4AjUWIQA/Gnt2VFhiQlJPd11dXmJWd1p/THNYflR1fV9DaUwxOX1dQl9gSXtSdkRlXX9beXRRQmE=;MjAwMDkyNUAzMjMxMmUzMjJlMzNjU2Z2TmczMkM5amhJTExrNzdVM3lERjhxSXVVUW83WmVjUW92NC8xNzQ4PQ==;MjAwMDkyNkAzMjMxMmUzMjJlMzNMWCt0dUhIeUVTZGN1U05xdUdjVVVkdEhQLzEvaDJrdWM1UmFtS3RzcnlnPQ==;NRAiBiAaIQQuGjN/V0d+Xk9HfV5AQmBIYVp/TGpJfl96cVxMZVVBJAtUQF1hSn5Wd0ViX3tedX1UR2ZV;MjAwMDkyOEAzMjMxMmUzMjJlMzNMWEdvc0YzQnMwTnBzNjQyVmVmc2FjSXUzLy82a3lPaDJpSlJ6VUJ0N21BPQ==;MjAwMDkyOUAzMjMxMmUzMjJlMzNUMzdIVXc4emNRRjMvS1pRZEFsYjNpMWNoNmlEWmVueXU5ZDRzNGZoUHRFPQ==;Mgo+DSMBMAY9C3t2VFhiQlJPd11dXmJWd1p/THNYflR1fV9DaUwxOX1dQl9gSXtSdkRlXX9beXVcRWQ=;MjAwMDkzMUAzMjMxMmUzMjJlMzNuWUtkTkRBMVVZU0pFalJUYUxoOUVuQWVZSllqbVNvUWtJMU14SW51VjNvPQ==;MjAwMDkzMkAzMjMxMmUzMjJlMzNORzMySEw2a3cwb1B5aUI0bGVzNnhoMjNtOGxCaEJnWUVqNEtCcVJYSTVVPQ==;MjAwMDkzM0AzMjMxMmUzMjJlMzNMWEdvc0YzQnMwTnBzNjQyVmVmc2FjSXUzLy82a3lPaDJpSlJ6VUJ0N21BPQ==");
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
