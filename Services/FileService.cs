using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using EnvioSafTApp.Services.Interfaces;

namespace EnvioSafTApp.Services
{
    public class FileService : IFileService
    {
        public async Task<string?> OpenFileDialogAsync(string filter)
        {
            var window = GetMainWindow();
            if (window?.StorageProvider == null)
            {
                return null;
            }

            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = ParseFileTypes(filter)
            });

            return files.FirstOrDefault()?.TryGetLocalPath();
        }

        public async Task<string?> SaveFileDialogAsync(string filter, string defaultExt)
        {
            var window = GetMainWindow();
            if (window?.StorageProvider == null)
            {
                return null;
            }

            var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = "output",
                DefaultExtension = NormalizeExtension(defaultExt),
                FileTypeChoices = ParseFileTypes(filter)
            });

            return file?.TryGetLocalPath();
        }

        private static Window? GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }

            return null;
        }

        private static List<FilePickerFileType> ParseFileTypes(string filter)
        {
            var result = new List<FilePickerFileType>();
            if (string.IsNullOrWhiteSpace(filter))
            {
                return result;
            }

            var chunks = filter.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var i = 0; i + 1 < chunks.Length; i += 2)
            {
                var label = chunks[i];
                var patterns = chunks[i + 1]
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(NormalizeGlob)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (patterns.Count == 0)
                {
                    continue;
                }

                result.Add(new FilePickerFileType(label)
                {
                    Patterns = patterns
                });
            }

            return result;
        }

        private static string NormalizeGlob(string pattern)
        {
            var value = pattern.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (!value.StartsWith("*", StringComparison.Ordinal))
            {
                if (value.StartsWith(".", StringComparison.Ordinal))
                {
                    value = "*" + value;
                }
                else
                {
                    value = "*." + value;
                }
            }

            return value;
        }

        private static string? NormalizeExtension(string? extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return null;
            }

            return extension.StartsWith(".", StringComparison.Ordinal) ? extension[1..] : extension;
        }
    }
}
