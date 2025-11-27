using EnvioSafTApp.Models;
using System.Threading;
using System.Threading.Tasks;


namespace EnvioSafTApp.Services.Interfaces
{
    public interface IJarUpdateService
    {
        Task<JarUpdateResult> EnsureLatestAsync(CancellationToken cancellationToken);
        string GetLocalJarPath();
        Task<(string? SavedPath, bool IsNew)> RememberJarAsync(string? jarPath, CancellationToken cancellationToken);
    }
}
