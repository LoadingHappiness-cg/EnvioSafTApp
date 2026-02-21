using EnvioSafTApp.Services;
using System.Net;
using System.Net.Http;
using Xunit;

namespace EnvioSafTApp.Tests;

public sealed class JarUpdateServiceTests
{
    [Fact]
    public void GetLocalJarPath_PrefersUserLibs_AndFallsBackToBundleLibs()
    {
        var root = CreateTempDir();
        try
        {
            var userLibs = Path.Combine(root, "user-libs");
            var bundleLibs = Path.Combine(root, "bundle-libs");
            Directory.CreateDirectory(userLibs);
            Directory.CreateDirectory(bundleLibs);

            var bundleJar = Path.Combine(bundleLibs, "EnviaSaft.jar");
            File.WriteAllText(bundleJar, "bundle");

            var service = new JarUpdateService(userLibs, bundleLibs, null);

            var firstPath = service.GetLocalJarPath();
            Assert.Equal(bundleJar, firstPath);

            var userJar = Path.Combine(userLibs, "EnviaSaft.jar");
            File.WriteAllText(userJar, "user");

            var secondPath = service.GetLocalJarPath();
            Assert.Equal(userJar, secondPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetLocalJarPath_DoesNotThrow_WhenBundleLibsFolderDoesNotExist()
    {
        var root = CreateTempDir();
        try
        {
            var userLibs = Path.Combine(root, "user-libs");
            var missingBundleLibs = Path.Combine(root, "missing-bundle-libs");
            Directory.CreateDirectory(userLibs);

            var service = new JarUpdateService(userLibs, missingBundleLibs, null);

            var path = service.GetLocalJarPath();

            Assert.Equal(Path.Combine(userLibs, "EnviaSaft.jar"), path);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetLocalJarPath_UsesJarFromResourcesFolder_WhenBundleLibsIsMissing()
    {
        var root = CreateTempDir();
        try
        {
            var userLibs = Path.Combine(root, "user-libs");
            var bundleLibs = Path.Combine(root, "missing-bundle-libs");
            var resources = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
            Directory.CreateDirectory(userLibs);
            Directory.CreateDirectory(resources);

            var resourceJar = Path.Combine(resources, "FACTEMICLI-2.8.6-87748-cmdClient.jar");
            File.WriteAllText(resourceJar, "bundle-resource-jar");

            var service = new JarUpdateService(userLibs, bundleLibs, null);
            var jarPath = service.GetLocalJarPath();

            Assert.Equal(resourceJar, jarPath);
            File.Delete(resourceJar);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureLatestAsync_SeedsJarFromDownloads_WhenAtEndpointIsUnavailable()
    {
        var root = CreateTempDir();
        try
        {
            var userLibs = Path.Combine(root, "user-libs");
            var bundleLibs = Path.Combine(root, "bundle-libs");
            var downloads = Path.Combine(root, "downloads");
            Directory.CreateDirectory(userLibs);
            Directory.CreateDirectory(bundleLibs);
            Directory.CreateDirectory(downloads);

            var downloadedJar = Path.Combine(downloads, "EnviaSaft_Official.jar");
            await File.WriteAllTextAsync(downloadedJar, "jar-content");

            using var httpClient = new HttpClient(new StaticStatusHandler(HttpStatusCode.NotFound));
            var service = new JarUpdateService(userLibs, bundleLibs, httpClient, new[] { downloads });

            var result = await service.EnsureLatestAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(result.UsedFallback);
            Assert.True(File.Exists(result.JarPath));
            Assert.Contains("EnviaSaft_Official.jar", result.JarPath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "jar-update-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class StaticStatusHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(statusCode));
    }
}
