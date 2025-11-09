using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace EnvioSafTApp.Models
{
    public class EnvioHistoricoEntry
    {
        public string NIF { get; set; } = "";
        public string EmpresaNome { get; set; } = "";
        public string FicheiroSaft { get; set; } = "";
        public string FicheiroOutput { get; set; } = "";
        public string Operacao { get; set; } = "";
        public DateTime DataHora { get; set; }
        public string Resultado { get; set; } = "";
        public string? Resumo { get; set; }
        public string? LogFilePath { get; set; }
        public List<string> Tags { get; set; } = new();

        public int Ano { get; set; } = 0;
        public string Mes { get; set; } = "";

        [JsonIgnore]
        public string ResultadoIcone => Resultado switch
        {
            "sucesso" => "✅",
            "erro" => "❌",
            "teste" => "🧪", // ou "T" se preferires letra
            "atualizacao" => "🔄",
            _ => "❓"
        };

        [JsonIgnore]
        public Brush ResultadoCor => Resultado switch
        {
            "sucesso" => Brushes.Green,
            "erro" => Brushes.Red,
            "teste" => Brushes.DarkOrange,
            "atualizacao" => Brushes.Goldenrod,
            _ => Brushes.Gray
        };

        [JsonIgnore]
        public string TagsDisplay => Tags != null && Tags.Any()
            ? string.Join(", ", Tags)
            : "—";
    }
}