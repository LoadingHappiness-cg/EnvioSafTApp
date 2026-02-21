using EnvioSafTApp.Services;
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

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "jar-update-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
