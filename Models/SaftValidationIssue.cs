using System.Globalization;

namespace EnvioSafTApp.Models
{
    public class SaftValidationIssue
    {
        public string Severidade { get; set; } = "Erro";
        public string Mensagem { get; set; } = string.Empty;
        public string? Sugestao { get; set; }
        public int? Linha { get; set; }
        public int? Coluna { get; set; }

        public string Resumo => ConstruirResumo();

        public string ConstruirResumo()
        {
            var localizacao = (Linha.HasValue && Coluna.HasValue)
                ? $"Linha {Linha.Value.ToString(CultureInfo.InvariantCulture)}, Coluna {Coluna.Value.ToString(CultureInfo.InvariantCulture)}"
                : Linha.HasValue ? $"Linha {Linha.Value.ToString(CultureInfo.InvariantCulture)}" : string.Empty;

            if (string.IsNullOrWhiteSpace(localizacao))
            {
                return Mensagem;
            }

            return $"{localizacao}: {Mensagem}";
        }
    }
}
