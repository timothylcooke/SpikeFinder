using ReactiveUI;
using SpikeFinder.ViewModels;
using SpikeFinder.Views;
using Splat;
using System.Reflection;

namespace SpikeFinder
{
    public class AppBootstrapper : IScreen
    {
        public RoutingState Router { get; }

        public AppBootstrapper()
        {
            Router = new RoutingState();

            Locator.CurrentMutable.RegisterLazySingleton(() => this);

            Locator.CurrentMutable.RegisterLazySingleton(() => new MainWindowViewModel());
            Locator.CurrentMutable.RegisterLazySingleton(() => new MainWindow());

            Locator.CurrentMutable.RegisterViewsForViewModels(Assembly.GetEntryAssembly()!);
        }
    }
}
