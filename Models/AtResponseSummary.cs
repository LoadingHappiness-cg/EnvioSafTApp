using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnvioSafTApp.Models
{
    public class AtResponseSummary
    {
        public bool Sucesso { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string MensagemPrincipal { get; set; } = string.Empty;
        public List<string> Erros { get; set; } = new();
        public List<string> Avisos { get; set; } = new();
        public List<string> Codigos { get; set; } = new();
        public string OutputCompleto { get; set; } = string.Empty;
        public string OutputErro { get; set; } = string.Empty;

        public string ConstruirResumoLegivel()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Estado: {Estado}");
            if (!string.IsNullOrWhiteSpace(MensagemPrincipal))
            {
                sb.AppendLine(MensagemPrincipal);
            }

            if (Erros.Any())
            {
                sb.AppendLine("Erros:");
                foreach (var erro in Erros)
                {
                    sb.AppendLine($" • {erro}");
                }
            }

            if (Avisos.Any())
            {
                sb.AppendLine("Avisos:");
                foreach (var aviso in Avisos)
                {
                    sb.AppendLine($" • {aviso}");
                }
            }

            if (Codigos.Any())
            {
                sb.AppendLine("Códigos da AT:");
                foreach (var codigo in Codigos.Distinct())
                {
                    sb.AppendLine($" • {codigo}");
                }
            }

            return sb.ToString().Trim();
        }
    }
}
