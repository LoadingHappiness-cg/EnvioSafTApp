using EnvioSafTApp.Services.Interfaces;
using Microsoft.Win32;

namespace EnvioSafTApp.Services
{
    public class FileService : IFileService
    {
        public string? OpenFileDialog(string filter)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter
            };

            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }

            return null;
        }

        public string? SaveFileDialog(string filter, string defaultExt)
        {
            var dialog = new SaveFileDialog
            {
                Filter = filter,
                DefaultExt = defaultExt
            };

            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }

            return null;
        }
    }
}
