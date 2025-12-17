using Microsoft.Extensions.DependencyInjection;
using EnvioSafTApp.Services;
using EnvioSafTApp.Services.Interfaces;
using EnvioSafTApp.ViewModels;
using System;
using System.Windows;


namespace EnvioSafTApp
{
    public partial class App : Application
    {
        public IServiceProvider ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            ServiceProvider = serviceCollection.BuildServiceProvider();

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Services
            services.AddSingleton<IJarUpdateService, JarUpdateService>();
            services.AddSingleton<IHistoricoEnviosService, HistoricoEnviosService>();
            services.AddSingleton<IPreflightCheckService, PreflightCheckService>();
            services.AddSingleton<ISaftValidationService, SaftValidationService>();
            services.AddSingleton<IFileService, FileService>();

            // ViewModels
            services.AddTransient<MainViewModel>();

            // Windows
            services.AddTransient<MainWindow>();
        }
    }
}
