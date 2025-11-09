using EnvioSafTApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EnvioSafTApp.Services
{
    public static class AtResponseInterpreter
    {
        private static readonly Regex CodigoRegex = new(@"AT[0-9]{4,6}|(?:\b[A-Z]{2}\d{3,}\b)", RegexOptions.IgnoreCase);

        public static AtResponseSummary Interpret(string stdout, string stderr)
        {
            var summary = new AtResponseSummary
            {
                OutputCompleto = stdout ?? string.Empty,
                OutputErro = stderr ?? string.Empty
            };

            var linhas = (stdout ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            var linhasErro = (stderr ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            foreach (var linha in linhas.Concat(linhasErro))
            {
                var codigoMatch = CodigoRegex.Match(linha);
                if (codigoMatch.Success)
                {
                    summary.Codigos.Add(codigoMatch.Value);
                }

                if (linha.Contains("erro", StringComparison.OrdinalIgnoreCase))
                {
                    summary.Erros.Add(linha);
                }
                else if (linha.Contains("aviso", StringComparison.OrdinalIgnoreCase) || linha.Contains("warning", StringComparison.OrdinalIgnoreCase))
                {
                    summary.Avisos.Add(linha);
                }
            }

            summary.Sucesso = !summary.Erros.Any() && linhas.Any(l => l.Contains("sucesso", StringComparison.OrdinalIgnoreCase) || l.Contains("enviado", StringComparison.OrdinalIgnoreCase));

            if (summary.Sucesso)
            {
                summary.Estado = "Sucesso";
                summary.MensagemPrincipal = linhas.FirstOrDefault(l => l.Contains("sucesso", StringComparison.OrdinalIgnoreCase)) ?? "Envio concluído.";
            }
            else if (summary.Erros.Any() || linhasErro.Any())
            {
                summary.Estado = "Erro";
                summary.MensagemPrincipal = summary.Erros.FirstOrDefault() ?? linhasErro.FirstOrDefault() ?? "Ocorreu um erro durante o envio.";
            }
            else if (summary.Avisos.Any())
            {
                summary.Estado = "Concluído com avisos";
                summary.MensagemPrincipal = summary.Avisos.First();
            }
            else
            {
                summary.Estado = string.IsNullOrWhiteSpace(stdout) && string.IsNullOrWhiteSpace(stderr) ? "Sem resposta" : "Resultado indeterminado";
                summary.MensagemPrincipal = linhas.FirstOrDefault() ?? linhasErro.FirstOrDefault() ?? string.Empty;
            }

            return summary;
        }
    }
}
