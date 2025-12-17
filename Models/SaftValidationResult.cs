using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnvioSafTApp.Models
{
    public class SaftValidationResult
    {
        public bool Sucesso { get; set; }
        public bool EsquemaDisponivel { get; set; }
        public string Resumo { get; set; } = string.Empty;
        public string OrigemXsd { get; set; } = string.Empty;
        public string? MensagemEstado { get; set; }
        public List<SaftValidationIssue> Problemas { get; set; } = new();
        public List<string> Sugestoes { get; set; } = new();

        public int TotalErros => Problemas.Count(p => p.Severidade == "Erro");
        public int TotalAvisos => Problemas.Count(p => p.Severidade != "Erro");

        public string ConstruirResumoLegivel()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Resumo);

            if (Problemas.Any())
            {
                sb.AppendLine();
                sb.AppendLine("Detalhes:");
                foreach (var issue in Problemas)
                {
                    sb.AppendLine($" • [{issue.Severidade}] {issue.ConstruirResumo()}");
                    if (!string.IsNullOrWhiteSpace(issue.Sugestao))
                    {
                        sb.AppendLine($"    Sugestão: {issue.Sugestao}");
                    }
                }
            }

            if (Sugestoes.Any())
            {
                sb.AppendLine();
                sb.AppendLine("Sugestões gerais:");
                foreach (var sugestao in Sugestoes.Distinct())
                {
                    sb.AppendLine($" • {sugestao}");
                }
            }

            return sb.ToString().Trim();
        }
    }
}
