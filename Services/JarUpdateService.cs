using EnvioSafTApp.Models;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EnvioSafTApp.Services
{
    public static class JarUpdateService
    {
        private const string JarFileName = "EnviaSaft.jar";
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

            string jarPath = Path.Combine(libsFolder, JarFileName);
            string metadataPath = Path.Combine(libsFolder, MetadataFileName);

            var result = new JarUpdateResult
            {
                JarPath = jarPath
            };

            JarMetadata? metadata = await LoadMetadataAsync(metadataPath, cancellationToken);

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
                        result.Message = $"O EnviaSaft.jar já estava atualizado (última atualização em {data}).";
                    }
                    else
                    {
                        result.Message = "O EnviaSaft.jar já estava atualizado.";
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
                    result.Message = result.Success
                        ? "⚠️ Não foi possível verificar atualizações do EnviaSaft.jar; a versão existente será utilizada."
                        : "Não foi possível descarregar o EnviaSaft.jar a partir da AT.";
                    result.ErrorMessage = $"Falha HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
                    return result;
                }

                await SaveJarAsync(response, jarPath, metadataPath, cancellationToken);

                result.Success = true;
                result.Updated = true;
                result.RemoteLastModified = response.Content.Headers.LastModified;

                if (result.RemoteLastModified.HasValue)
                {
                    var data = result.RemoteLastModified.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                    result.Message = $"Foi descarregada a versão mais recente do EnviaSaft.jar (atualizado em {data}).";
                }
                else
                {
                    result.Message = "Foi descarregada a versão mais recente do EnviaSaft.jar.";
                }
                return result;
            }
            catch (Exception ex)
            {
                result.Success = File.Exists(jarPath);
                result.UsedFallback = result.Success;
                result.Updated = false;
                result.Message = result.Success
                    ? "⚠️ Não foi possível confirmar atualizações do EnviaSaft.jar; a versão local será utilizada."
                    : "Não foi possível preparar o EnviaSaft.jar. Verifique a ligação à internet ou faça a atualização manual.";
                result.ErrorMessage = ex.Message;
                return result;
            }
            finally
            {
                response?.Dispose();
            }
        }

        private static async Task SaveJarAsync(HttpResponseMessage response, string jarPath, string metadataPath, CancellationToken cancellationToken)
        {
            string tempFile = Path.GetTempFileName();

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
                LastModified = response.Content.Headers.LastModified
            };

            await SaveMetadataAsync(metadataPath, metadata, cancellationToken);
        }

        private static async Task<JarMetadata?> LoadMetadataAsync(string metadataPath, CancellationToken cancellationToken)
        {
            if (!File.Exists(metadataPath))
            {
                return null;
            }

            await using var stream = File.OpenRead(metadataPath);
            return await JsonSerializer.DeserializeAsync<JarMetadata>(stream, cancellationToken: cancellationToken);
        }

        private static async Task SaveMetadataAsync(string metadataPath, JarMetadata metadata, CancellationToken cancellationToken)
        {
            await using var stream = File.Open(metadataPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, metadata, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
        }

        private class JarMetadata
        {
            public string? ETag { get; set; }
            public DateTimeOffset? LastModified { get; set; }
        }
    }
}
