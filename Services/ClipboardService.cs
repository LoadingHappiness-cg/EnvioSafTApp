using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using EnvioSafTApp.Services.Interfaces;

namespace EnvioSafTApp.Services
{
    public class ClipboardService : IClipboardService
    {
        public async Task<bool> SetTextAsync(string text, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return false;
            }

            Window? window = desktop.MainWindow;
            var topLevel = window != null ? TopLevel.GetTopLevel(window) : null;
            if (topLevel?.Clipboard == null)
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await topLevel.Clipboard.SetTextAsync(text);
            return true;
        }
    }
}
