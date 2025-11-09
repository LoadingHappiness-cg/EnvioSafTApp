using EnvioSafTApp.Models;
using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EnvioSafTApp.Services
{
    public static class JarUpdateService
    {
        private const string DefaultJarFileName = "EnviaSaft.jar";
        private const string MetadataFileName = "EnviaSaft.jar.metadata.json";
        private static readonly Uri JarDownloadUri = new("https://www.portaldasfinancas.gov.pt/static/docs/factemi/EnviaSaft.jar");
        private static readonly HttpClient HttpClient = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(120)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("EnvioSaftApp/1.0");
            return client;
        }

        public static async Task<JarUpdateResult> EnsureLatestAsync(CancellationToken cancellationToken)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string libsFolder = Path.Combine(baseDirectory, "libs");
            Directory.CreateDirectory(libsFolder);

            string metadataPath = Path.Combine(libsFolder, MetadataFileName);
            JarMetadata? metadata = await LoadMetadataAsync(metadataPath, cancellationToken);
            string jarPath = ResolveExistingJarPath(libsFolder, metadata);

            var result = new JarUpdateResult
            {
                JarPath = jarPath
            };

            HttpResponseMessage? response = null;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, JarDownloadUri);
                if (!string.IsNullOrEmpty(metadata?.ETag))
                {
                    request.Headers.TryAddWithoutValidation("If-None-Match", metadata.ETag);
                }
                else if (metadata?.LastModified is DateTimeOffset lastModified)
                {
                    request.Headers.IfModifiedSince = lastModified;
                }

                response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotModified && File.Exists(jarPath))
                {
                    result.Success = true;
                    result.Updated = false;
                    result.RemoteLastModified = metadata?.LastModified;

                    if (result.RemoteLastModified.HasValue)
                    {
                        var data = result.RemoteLastModified.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                        result.Message = $"O {Path.GetFileName(jarPath)} já estava atualizado (última atualização em {data}).";
                    }
                    else
                    {
                        result.Message = $"O {Path.GetFileName(jarPath)} já estava atualizado.";
                    }
                    return result;
                }

                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    response.Dispose();
                    response = await HttpClient.GetAsync(JarDownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                }

                if (!response.IsSuccessStatusCode)
                {
                    result.Success = File.Exists(jarPath);
                    result.UsedFallback = result.Success;
                    result.Updated = false;
                    string fileName = Path.GetFileName(jarPath);
                    result.Message = result.Success
                        ? $"⚠️ Não foi possível verificar atualizações do {fileName}; a versão existente será utilizada."
                        : $"Não foi possível descarregar o {fileName} a partir da AT.";
                    result.ErrorMessage = $"Falha HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
                    return result;
                }

                string savedPath = await SaveJarAsync(response, libsFolder, metadataPath, metadata, cancellationToken);

                result.Success = true;
                result.Updated = true;
                result.JarPath = savedPath;
                result.RemoteLastModified = response.Content.Headers.LastModified;

                string savedFileName = Path.GetFileName(savedPath);
                if (result.RemoteLastModified.HasValue)
                {
                    var data = result.RemoteLastModified.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                    result.Message = $"Foi descarregada a versão mais recente do {savedFileName} (atualizado em {data}).";
                }
                else
                {
                    result.Message = $"Foi descarregada a versão mais recente do {savedFileName}.";
                }
                return result;
            }
            catch (Exception ex)
            {
                jarPath = ResolveExistingJarPath(libsFolder, metadata);
                result.JarPath = jarPath;
                result.Success = File.Exists(jarPath);
                result.UsedFallback = result.Success;
                result.Updated = false;
                string fileName = Path.GetFileName(jarPath);
                result.Message = result.Success
                    ? $"⚠️ Não foi possível confirmar atualizações do {fileName}; a versão local será utilizada."
                    : $"Não foi possível preparar o {fileName}. Verifique a ligação à internet ou faça a atualização manual.";
                result.ErrorMessage = ex.Message;
                return result;
            }
            finally
            {
                response?.Dispose();
            }
        }

        private static string ResolveExistingJarPath(string libsFolder, JarMetadata? metadata)
        {
            if (metadata != null && !string.IsNullOrWhiteSpace(metadata.FileName))
            {
                string candidatePath = Path.Combine(libsFolder, metadata.FileName);
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            string defaultPath = Path.Combine(libsFolder, DefaultJarFileName);
            if (File.Exists(defaultPath))
            {
                return defaultPath;
            }

            string? firstJar = Directory.EnumerateFiles(libsFolder, "*.jar")
                .OrderBy(f => f)
                .FirstOrDefault();

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
                LastModified = response.Content.Headers.LastModified,
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

        public static string GetLocalJarPath()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string libsFolder = Path.Combine(baseDirectory, "libs");
            Directory.CreateDirectory(libsFolder);

            string metadataPath = Path.Combine(libsFolder, MetadataFileName);
            JarMetadata? metadata = LoadMetadata(metadataPath);

            string jarPath = ResolveExistingJarPath(libsFolder, metadata);
            return jarPath;
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
