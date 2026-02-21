using Microsoft.Extensions.DependencyInjection;
using EnvioSafTApp.Services;
using EnvioSafTApp.Services.Interfaces;
using EnvioSafTApp.ViewModels;
using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace EnvioSafTApp
{
    public partial class App : Application
    {
        public static IServiceProvider? ServiceProvider { get; private set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            ServiceProvider = serviceCollection.BuildServiceProvider();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Services
            services.AddSingleton<IJarUpdateService, JarUpdateService>();
            services.AddSingleton<IHistoricoEnviosService, HistoricoEnviosService>();
            services.AddSingleton<IPreflightCheckService, PreflightCheckService>();
            services.AddSingleton<ISaftValidationService, SaftValidationService>();
            services.AddSingleton<IFileService, FileService>();
            services.AddSingleton<IClipboardService, ClipboardService>();

            // ViewModels
            services.AddTransient<MainViewModel>();

            // Windows
            services.AddTransient<MainWindow>();
        }
    }
}
