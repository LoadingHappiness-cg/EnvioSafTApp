using EnvioSafTApp.ViewModels;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace EnvioSafTApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindow(MainViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
            Opened += MainWindow_Opened;
        }

        private async void MainWindow_Opened(object? sender, System.EventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                await vm.RunPreflightCheckCommand.ExecuteAsync(null);
            }
        }

        private void RightTabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl tc && tc.SelectedItem is TabItem selectedTab && selectedTab.Name == "HistoricoTab")
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.CarregarHistorico();
                }
            }
        }

        private void Hyperlink_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is string uri)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = uri,
                        UseShellExecute = true
                    });
                }
                e.Handled = true;
            }
            catch
            {
                // Silently fail if browser can't be opened
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
