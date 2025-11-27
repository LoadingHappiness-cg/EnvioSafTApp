using System.Collections.Generic;
using System.Threading.Tasks;
using EnvioSafTApp.Models;

namespace EnvioSafTApp.Services.Interfaces
{
    public interface IPreflightCheckService
    {
        Task<IReadOnlyList<PreflightCheckResult>> RunAsync();
    }
}
