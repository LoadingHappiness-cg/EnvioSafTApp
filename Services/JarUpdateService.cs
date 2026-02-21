using EnvioSafTApp.Models;
using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using EnvioSafTApp.Services.Interfaces;

namespace EnvioSafTApp.Services
{
    public class JarUpdateService : IJarUpdateService
    {
        private const string DefaultJarFileName = "EnviaSaft.jar";
        private const string MetadataFileName = "EnviaSaft.jar.metadata.json";
        private static readonly Uri JarDownloadUri = new Uri("https://www.portaldasfinancas.gov.pt/static/docs/factemi/EnviaSaft.jar");
        private readonly HttpClient _httpClient;
        private readonly string _userLibsFolder;
        private readonly string _bundleLibsFolder;
        private readonly IReadOnlyList<string> _seedSearchFolders;
        private static readonly Regex JarExecutionRegex = new Regex(@"java\s+-jar\s+(?:(['""])(?<path>[^'""]+)\1|(?<path>[^\s]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public JarUpdateService()
            : this(userLibsFolder: null, bundleLibsFolder: null, httpClient: null, seedSearchFolders: null)
        {
        }

        public JarUpdateService(string? userLibsFolder, string? bundleLibsFolder, HttpClient? httpClient, IEnumerable<string>? seedSearchFolders = null)
        {
            _httpClient = httpClient ?? CreateClient();
            _userLibsFolder = string.IsNullOrWhiteSpace(userLibsFolder)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EnviaSaft", "libs")
                : userLibsFolder;
            _bundleLibsFolder = string.IsNullOrWhiteSpace(bundleLibsFolder)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libs")
                : bundleLibsFolder;
            _seedSearchFolders = (seedSearchFolders ?? GetDefaultSeedSearchFolders())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(120)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh; Intel Mac OS X 14_0) EnvioSaftApp/2.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
            return client;
        }

        public async Task<JarUpdateResult> EnsureLatestAsync(CancellationToken cancellationToken)
        {
            string libsFolder = GetUserLibsFolder();
            Directory.CreateDirectory(libsFolder);

            string metadataPath = Path.Combine(libsFolder, MetadataFileName);
            JarMetadata? metadata = await LoadMetadataAsync(metadataPath, cancellationToken);
            string jarPath = ResolveExistingJarPath(libsFolder, metadata);
            bool existedInitially = File.Exists(jarPath);
            bool seededFromBundle = false;
            bool seededFromKnownLocation = false;

            if (!existedInitially)
            {
                var seededJar = await TrySeedFromBundleAsync(libsFolder, metadataPath, cancellationToken);
                if (!string.IsNullOrWhiteSpace(seededJar))
                {
                    jarPath = seededJar;
                    seededFromBundle = true;
                }
            }

            if (!File.Exists(jarPath))
            {
                var discoveredJar = await TrySeedFromKnownLocationsAsync(libsFolder, metadataPath, cancellationToken);
                if (!string.IsNullOrWhiteSpace(discoveredJar))
                {
                    jarPath = discoveredJar;
                    seededFromKnownLocation = true;
                }
            }

            if (File.Exists(jarPath))
            {
                await EnsureMetadataMatchesLocalJarAsync(metadataPath, metadata, jarPath, cancellationToken);

                var readyResult = new JarUpdateResult
                {
                    Success = true,
                    Updated = false,
                    JarPath = jarPath,
                    UsedFallback = !existedInitially
                };

                var fileName = Path.GetFileName(jarPath);
                if (seededFromBundle)
                {
                    readyResult.Message = $"Cliente local preparado com {fileName} incluído na aplicação.";
                }
                else if (seededFromKnownLocation)
                {
                    readyResult.Message = $"Cliente local preparado com {fileName} encontrado em pasta do utilizador.";
                }
                else
                {
                    readyResult.Message = $"Cliente local pronto: {fileName}. Atualizações serão geridas pelo cliente oficial da AT durante o envio.";
                }

                return readyResult;
            }

            return new JarUpdateResult
            {
                Success = false,
                Updated = false,
                UsedFallback = false,
                JarPath = jarPath,
                Message = $"Não foi possível preparar o {Path.GetFileName(jarPath)}. Coloque manualmente o ficheiro oficial da AT em '{libsFolder}' ou em Downloads/Documentos/Desktop."
            };
        }

        private static string ResolveExistingJarPath(string libsFolder, JarMetadata? metadata)
        {
            string defaultPath = Path.Combine(libsFolder, DefaultJarFileName);
            if (!Directory.Exists(libsFolder))
            {
                return defaultPath;
            }

            if (metadata != null && !string.IsNullOrWhiteSpace(metadata.FileName))
            {
                string candidatePath = Path.Combine(libsFolder, metadata.FileName);
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            if (File.Exists(defaultPath))
            {
                return defaultPath;
            }

            string? firstJar = null;
            try
            {
                firstJar = Directory.EnumerateFiles(libsFolder, "*.jar")
                    .OrderBy(f => f)
                    .FirstOrDefault();
            }
            catch
            {
                // Ignorar falhas de enumeração e usar caminho default.
            }

            return firstJar ?? defaultPath;
        }

        private static async Task<string> SaveJarAsync(HttpResponseMessage response, string libsFolder, string metadataPath, JarMetadata? previousMetadata, CancellationToken cancellationToken)
        {
            string tempFile = Path.GetTempFileName();
            string? remoteFileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName;
            if (!string.IsNullOrWhiteSpace(remoteFileName))
            {
                remoteFileName = remoteFileName.Trim('"');
            }

            string fileName = !string.IsNullOrWhiteSpace(remoteFileName)
                ? remoteFileName
                : previousMetadata?.FileName ?? DefaultJarFileName;

            string jarPath = Path.Combine(libsFolder, fileName);

            try
            {
                await using (var fs = File.Open(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs, cancellationToken);
                }

                if (File.Exists(jarPath))
                {
                    string backupPath = jarPath + ".bak";
                    try
                    {
                        File.Copy(jarPath, backupPath, overwrite: true);
                    }
                    catch
                    {
                        // Ignorar falhas ao criar backup; não deve bloquear a atualização.
                    }
                }

                File.Copy(tempFile, jarPath, true);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch
                    {
                        // Ignorar falhas ao remover o temporário.
                    }
                }
            }

            var metadata = new JarMetadata
            {
                ETag = response.Headers.ETag?.Tag,
                LastModified = response.Content.Headers.LastModified ?? GetFileLastWriteTimeUtc(jarPath),
                FileName = fileName
            };

            await SaveMetadataAsync(metadataPath, metadata, cancellationToken);

            // Limpar ficheiros obsoletos guardados com nomes anteriores.
            if (previousMetadata != null && !string.IsNullOrWhiteSpace(previousMetadata.FileName) && !string.Equals(previousMetadata.FileName, fileName, StringComparison.OrdinalIgnoreCase))
            {
                string previousPath = Path.Combine(libsFolder, previousMetadata.FileName);
                if (!string.Equals(previousPath, jarPath, StringComparison.OrdinalIgnoreCase) && File.Exists(previousPath))
                {
                    try
                    {
                        File.Delete(previousPath);
                    }
                    catch
                    {
                        // Ignorar falhas na limpeza; não bloqueia a atualização.
                    }
                }
            }

            return jarPath;
        }

        private static async Task<JarMetadata?> LoadMetadataAsync(string metadataPath, CancellationToken cancellationToken)
        {
            if (!File.Exists(metadataPath))
            {
                return null;
            }

            try
            {
                await using var stream = File.OpenRead(metadataPath);
                return await JsonSerializer.DeserializeAsync<JarMetadata>(stream, cancellationToken: cancellationToken);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static async Task SaveMetadataAsync(string metadataPath, JarMetadata metadata, CancellationToken cancellationToken)
        {
            await using var stream = File.Open(metadataPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, metadata, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
        }

        public static string? ExtractJarPathFromOutput(string? output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            string? lastPath = null;
            foreach (Match match in JarExecutionRegex.Matches(output))
            {
                if (match.Success)
                {
                    var candidate = match.Groups["path"].Value;
                    if (!string.IsNullOrWhiteSpace(candidate))
                    {
                        lastPath = candidate.Trim();
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(lastPath))
            {
                return null;
            }

            return NormalizePath(lastPath);
        }

        public async Task<(string? SavedPath, bool IsNew)> RememberJarAsync(string? jarPath, CancellationToken cancellationToken)
        {
            string? normalized = NormalizePath(jarPath);
            if (string.IsNullOrWhiteSpace(normalized) || !File.Exists(normalized))
            {
                return (null, false);
            }

            string libsFolder = GetUserLibsFolder();
            Directory.CreateDirectory(libsFolder);

            cancellationToken.ThrowIfCancellationRequested();

            string metadataPath = Path.Combine(libsFolder, MetadataFileName);
            JarMetadata? existingMetadata = LoadMetadata(metadataPath);
            string fileName = Path.GetFileName(normalized);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return (null, false);
            }

            string destinationPath = Path.Combine(libsFolder, fileName);
            string? originalPath = normalized;
            bool existedBefore = File.Exists(destinationPath);

            if (!PathsAreSame(normalized, destinationPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Copy(normalized, destinationPath, overwrite: true);
                normalized = destinationPath;
            }
            else
            {
                destinationPath = normalized;
            }

            var metadata = new JarMetadata
            {
                FileName = Path.GetFileName(destinationPath),
                LastModified = GetFileLastWriteTimeUtc(destinationPath)
            };

            bool metadataChanged = existingMetadata == null
                || !string.Equals(existingMetadata.FileName, metadata.FileName, StringComparison.OrdinalIgnoreCase)
                || existingMetadata.LastModified != metadata.LastModified;

            await SaveMetadataAsync(metadataPath, metadata, cancellationToken);
            bool changedLocation = !string.IsNullOrWhiteSpace(originalPath) && !PathsAreSame(originalPath, destinationPath);
            bool isNew = !existedBefore || changedLocation || metadataChanged;
            return (destinationPath, isNew);
        }

        private static async Task<JarMetadata?> EnsureMetadataMatchesLocalJarAsync(string metadataPath, JarMetadata? currentMetadata, string jarPath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string fileName = Path.GetFileName(jarPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return currentMetadata;
            }

            if (currentMetadata != null && string.Equals(currentMetadata.FileName, fileName, StringComparison.OrdinalIgnoreCase))
            {
                return currentMetadata;
            }

            var metadata = new JarMetadata
            {
                FileName = fileName,
                LastModified = GetFileLastWriteTimeUtc(jarPath)
            };

            await SaveMetadataAsync(metadataPath, metadata, cancellationToken);
            return metadata;
        }

        private static DateTimeOffset? GetFileLastWriteTimeUtc(string path)
        {
            try
            {
                var lastWrite = File.GetLastWriteTimeUtc(path);
                if (lastWrite <= DateTime.MinValue || lastWrite >= DateTime.MaxValue)
                {
                    return null;
                }

                return new DateTimeOffset(DateTime.SpecifyKind(lastWrite, DateTimeKind.Utc));
            }
            catch
            {
                return null;
            }
        }

        private static string? NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                string expanded = Environment.ExpandEnvironmentVariables(path.Trim());
                return Path.GetFullPath(expanded);
            }
            catch
            {
                return path.Trim();
            }
        }

        private static bool PathsAreSame(string first, string second)
        {
            try
            {
                string normalizedFirst = Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string normalizedSecond = Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.Equals(normalizedFirst, normalizedSecond, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
            }
        }

        public string GetLocalJarPath()
        {
            string libsFolder = GetUserLibsFolder();
            Directory.CreateDirectory(libsFolder);

            string metadataPath = Path.Combine(libsFolder, MetadataFileName);
            JarMetadata? metadata = LoadMetadata(metadataPath);

            string jarPath = ResolveExistingJarPath(libsFolder, metadata);
            if (File.Exists(jarPath))
            {
                return jarPath;
            }

            // Fallback para o .jar empacotado junto da app (libs ou Resources).
            string? bundleJar = ResolveJarFromFolders(GetBundleSearchFolders());
            if (!string.IsNullOrWhiteSpace(bundleJar) && File.Exists(bundleJar))
            {
                return bundleJar;
            }

            return jarPath;
        }

        private string GetUserLibsFolder() => _userLibsFolder;

        private string GetBundleLibsFolder() => _bundleLibsFolder;

        private IEnumerable<string> GetBundleSearchFolders()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var siblingResources = Path.GetFullPath(Path.Combine(baseDir, "..", "Resources"));
            return new[]
            {
                GetBundleLibsFolder(),
                Path.Combine(baseDir, "Resources"),
                siblingResources
            };
        }

        private static IEnumerable<string> GetDefaultSeedSearchFolders()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(userProfile))
            {
                yield break;
            }

            yield return Path.Combine(userProfile, "Downloads");
            yield return Path.Combine(userProfile, "Documents");
            yield return Path.Combine(userProfile, "Desktop");
        }

        private async Task<string?> TrySeedFromBundleAsync(string targetLibsFolder, string metadataPath, CancellationToken cancellationToken)
        {
            try
            {
                var sourceJar = ResolveJarFromFolders(GetBundleSearchFolders());

                if (string.IsNullOrWhiteSpace(sourceJar) || !File.Exists(sourceJar))
                {
                    return null;
                }

                var fileName = Path.GetFileName(sourceJar);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    return null;
                }

                var destinationJar = Path.Combine(targetLibsFolder, fileName);
                if (!PathsAreSame(sourceJar, destinationJar))
                {
                    File.Copy(sourceJar, destinationJar, overwrite: true);
                }

                var metadata = new JarMetadata
                {
                    FileName = fileName,
                    LastModified = GetFileLastWriteTimeUtc(destinationJar)
                };
                await SaveMetadataAsync(metadataPath, metadata, cancellationToken);
                return destinationJar;
            }
            catch
            {
                return null;
            }
        }

        private async Task<string?> TrySeedFromKnownLocationsAsync(string targetLibsFolder, string metadataPath, CancellationToken cancellationToken)
        {
            try
            {
                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = false,
                    IgnoreInaccessible = true,
                    MatchCasing = MatchCasing.CaseInsensitive
                };

                var candidates = new List<string>();
                foreach (var folder in _seedSearchFolders)
                {
                    if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                    {
                        continue;
                    }

                    candidates.AddRange(Directory.EnumerateFiles(folder, "*.jar", options)
                        .Where(file =>
                        {
                            var name = Path.GetFileName(file);
                            return !string.IsNullOrWhiteSpace(name)
                                && (name.Contains("envia", StringComparison.OrdinalIgnoreCase)
                                    || name.Contains("factemi", StringComparison.OrdinalIgnoreCase)
                                    || name.Contains("cmdclient", StringComparison.OrdinalIgnoreCase)
                                    || name.Contains("saft", StringComparison.OrdinalIgnoreCase));
                        }));
                }

                var selected = candidates
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
                    .FirstOrDefault();

                if (string.IsNullOrWhiteSpace(selected) || !File.Exists(selected))
                {
                    return null;
                }

                return await PersistSeededJarAsync(selected, targetLibsFolder, metadataPath, cancellationToken);
            }
            catch
            {
                return null;
            }
        }

        private static async Task<string?> PersistSeededJarAsync(string sourceJar, string targetLibsFolder, string metadataPath, CancellationToken cancellationToken)
        {
            var fileName = Path.GetFileName(sourceJar);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            Directory.CreateDirectory(targetLibsFolder);
            var destinationJar = Path.Combine(targetLibsFolder, fileName);
            if (!PathsAreSame(sourceJar, destinationJar))
            {
                File.Copy(sourceJar, destinationJar, overwrite: true);
            }

            var metadata = new JarMetadata
            {
                FileName = fileName,
                LastModified = GetFileLastWriteTimeUtc(destinationJar)
            };
            await SaveMetadataAsync(metadataPath, metadata, cancellationToken);
            return destinationJar;
        }

        private static string? ResolveJarFromFolders(IEnumerable<string> folders)
        {
            foreach (var folder in folders.Where(f => !string.IsNullOrWhiteSpace(f)))
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                var jar = Directory.EnumerateFiles(folder, "*.jar")
                    .OrderByDescending(path => ScoreJarName(Path.GetFileName(path)))
                    .ThenByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(jar) && File.Exists(jar))
                {
                    return jar;
                }
            }

            return null;
        }

        private static int ScoreJarName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return 0;
            }

            var name = fileName.ToLowerInvariant();
            var score = 0;
            if (name.Contains("envia")) score += 3;
            if (name.Contains("saft")) score += 3;
            if (name.Contains("factemi")) score += 3;
            if (name.Contains("cmdclient")) score += 2;
            return score;
        }

        private static JarMetadata? LoadMetadata(string metadataPath)
        {
            if (!File.Exists(metadataPath))
            {
                return null;
            }

            try
            {
                using var stream = File.OpenRead(metadataPath);
                return JsonSerializer.Deserialize<JarMetadata>(stream);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private class JarMetadata
        {
            public string? ETag { get; set; }
            public DateTimeOffset? LastModified { get; set; }
            public string? FileName { get; set; }
        }
    }
}
