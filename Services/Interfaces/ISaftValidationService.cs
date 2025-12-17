using System.Threading;
using System.Threading.Tasks;
using EnvioSafTApp.Models;

namespace EnvioSafTApp.Services.Interfaces
{
    public interface ISaftValidationService
    {
        Task<SaftValidationResult> ValidateAsync(string caminhoSafT, CancellationToken cancellationToken);
    }
}
