using System.Collections.Generic;
using EnvioSafTApp.Models;

namespace EnvioSafTApp.Services.Interfaces
{
    public interface IHistoricoEnviosService
    {
        string BaseFolder { get; }
        void RegistarEnvio(EnvioHistoricoEntry entry);
        string GuardarLog(EnvioHistoricoEntry entry, string resumo, string output, string error);
        List<EnvioHistoricoEntry> ObterHistorico();
        void ExportarCsv(IEnumerable<EnvioHistoricoEntry> entradas, string destino);
    }
}
