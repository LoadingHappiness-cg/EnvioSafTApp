using System.Threading;
using System.Threading.Tasks;

namespace EnvioSafTApp.Services.Interfaces
{
    public interface IClipboardService
    {
        Task<bool> SetTextAsync(string text, CancellationToken cancellationToken);
    }
}
