using EnvioSafTApp.Services;
using EnvioSafTApp.Services.Interfaces;
using EnvioSafTApp.ViewModels;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;


namespace EnvioSafTApp
{
    public partial class MainWindow : Window
    {
        private readonly IJarUpdateService _jarUpdateService;
        private readonly IPreflightCheckService _preflightCheckService;
        private readonly IHistoricoEnviosService _historicoEnviosService;
        private readonly StatusTickerService _ticker;


        public MainWindow(
            MainViewModel viewModel,
            IJarUpdateService jarUpdateService, 
            IPreflightCheckService preflightCheckService, 
            IHistoricoEnviosService historicoEnviosService)
        {
            DataContext = viewModel;
            _jarUpdateService = jarUpdateService;
            _preflightCheckService = preflightCheckService;
            _historicoEnviosService = historicoEnviosService;
            InitializeComponent();
            // AppVersionTextBlock.Text is now bound
            _ticker = new StatusTickerService(StatusTicker, StatusTickerIcon, StatusTickerBorder);
            // PreflightListView.ItemsSource is bound in XAML
            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                await vm.RunPreflightCheckCommand.ExecuteAsync(null);
            }
        }

        private void RightTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl && RightTabControl.SelectedItem is TabItem selectedTab && selectedTab.Header.ToString() == "Histórico")
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.CarregarHistorico();
                }
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Password = ((PasswordBox)sender).Password;
            }
        }

        private void ArquivoPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.ArquivoPassword = ((PasswordBox)sender).Password;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                // Silently fail if browser can't be opened
            }
        }
    }
}
