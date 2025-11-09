using System;

namespace EnvioSafTApp.Models
{
    public class PreflightCheckResult
    {
        public string Nome { get; set; } = string.Empty;
        public bool Sucesso { get; set; }
        public string Detalhes { get; set; } = string.Empty;
        public string ResolucaoSugerida { get; set; } = string.Empty;
        public DateTime DataExecucao { get; set; } = DateTime.Now;
    }
}
