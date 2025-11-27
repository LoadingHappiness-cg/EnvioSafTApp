using EnvioSafTApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EnvioSafTApp.Services
{
    public static class AtResponseInterpreter
    {
        private static readonly Regex CodigoRegex = new Regex(@"AT[0-9]{4,6}|(?:\b[A-Z]{2}\d{3,}\b)", RegexOptions.IgnoreCase);
        private static readonly Regex XmlTagRegex = new Regex(@"<[^>]+>");
        private static readonly Regex ClientUpdateCodeRegex = new Regex(@"code\s*=\s*""-9""", RegexOptions.IgnoreCase);
        private static readonly string[] NonErrorIndicators = new[]
        {
            "sem erro",
            "sem erros",
            "sem qualquer erro",
            "sem nenhum erro",
            "nenhum erro",
            "0 erro",
            "0 erros",
            "zero erros"
        };
        private static readonly string[] ClientUpdateIndicators = new[]
        {
            "necessita de atualizar o cliente de comando",
            "obtenção do jar",
            "ser iniciada a obtenção do jar",
            "nova versão",
            "obter o jar"
        };

        public static AtResponseSummary Interpret(string stdout, string stderr, int? exitCode = null, bool isTeste = false)
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

            bool requiresClientUpdate = false;
            List<string> clientUpdateMessages = new();

            foreach (var linha in linhas.Concat(linhasErro))
            {
                var codigoMatch = CodigoRegex.Match(linha);
                if (codigoMatch.Success)
                {
                    summary.Codigos.Add(codigoMatch.Value);
                }

                if (IsClientUpdateLine(linha))
                {
                    requiresClientUpdate = true;
                    var cleaned = CleanLine(linha);
                    if (!string.IsNullOrWhiteSpace(cleaned))
                    {
                        clientUpdateMessages.Add(cleaned);
                    }
                    continue;
                }

                if (IsErroLinha(linha))
                {
                    summary.Erros.Add(CleanLine(linha));
                }
                else if (linha.Contains("aviso", StringComparison.OrdinalIgnoreCase) || linha.Contains("warning", StringComparison.OrdinalIgnoreCase))
                {
                    summary.Avisos.Add(CleanLine(linha));
                }
            }

            // Check for test success indicators (totalFaturas, totalCreditos, etc.)
            bool hasTestSuccessIndicators = linhas.Any(l => 
                l.Contains("totalFaturas", StringComparison.OrdinalIgnoreCase) ||
                l.Contains("totalCreditos", StringComparison.OrdinalIgnoreCase) ||
                l.Contains("totalDocumentos", StringComparison.OrdinalIgnoreCase) ||
                l.Contains("processamento", StringComparison.OrdinalIgnoreCase));

            bool hasExplicitSuccess = linhas.Any(l => 
                l.Contains("sucesso", StringComparison.OrdinalIgnoreCase) || 
                l.Contains("enviado", StringComparison.OrdinalIgnoreCase) ||
                l.Contains("finished", StringComparison.OrdinalIgnoreCase));

            bool exitSuccess = exitCode.HasValue && exitCode.Value == 0;
            bool hasErrorOutput = summary.Erros.Any() || linhasErro.Any();

            // If we have test indicators or explicit success, and no errors, it's a success
            summary.Sucesso = !summary.Erros.Any() && (hasTestSuccessIndicators || hasExplicitSuccess);

            // Treat a clean exit code with no errors as success, even if the output is sparse
            if (!summary.Sucesso && exitSuccess && !hasErrorOutput)
            {
                summary.Sucesso = true;
            }

            if (requiresClientUpdate && !summary.Sucesso)
            {
                summary.RequerAtualizacaoCliente = true;
                summary.Estado = "Atualização necessária";
                var mensagensDistintas = clientUpdateMessages
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => m.Trim())
                    .Distinct()
                    .ToList();

                var mensagem = mensagensDistintas.Any()
                    ? string.Join(" ", mensagensDistintas)
                    : null;

                summary.MensagemPrincipal = mensagem ?? "A AT solicitou a atualização do cliente de comando. Volte a tentar após a atualização.";
            }
            else if (summary.Sucesso)
            {
                summary.Estado = isTeste ? "Teste concluído" : "Sucesso";
                // If it's a test response with statistics, use that as the message
                if (hasTestSuccessIndicators && !hasExplicitSuccess)
                {
                    summary.MensagemPrincipal = "Teste executado com sucesso. Ficheiro validado.";
                }
                else if (hasExplicitSuccess)
                {
                    summary.MensagemPrincipal = linhas.FirstOrDefault(l => l.Contains("sucesso", StringComparison.OrdinalIgnoreCase)) ?? "Envio concluído.";
                }
                else if (exitSuccess)
                {
                    summary.MensagemPrincipal = isTeste
                        ? "Teste concluído com sucesso. Ficheiro validado."
                        : "Envio concluído sem erros reportados.";
                }
                else
                {
                    summary.MensagemPrincipal = "Envio concluído.";
                }
            }
            else if (hasErrorOutput)
            {
                summary.Estado = "Erro";
                var mensagemErro = summary.Erros.FirstOrDefault() ?? linhasErro.FirstOrDefault();

                if (string.IsNullOrWhiteSpace(mensagemErro) && exitCode.HasValue)
                {
                    mensagemErro = $"O cliente de comando terminou com código {exitCode.Value}.";
                }

                summary.MensagemPrincipal = mensagemErro ?? "Ocorreu um erro durante o envio.";
            }
            else if (summary.Avisos.Any())
            {
                summary.Estado = "Concluído com avisos";
                summary.MensagemPrincipal = summary.Avisos.First();
            }
            else
            {
                if (exitCode.HasValue && exitCode.Value != 0)
                {
                    summary.Estado = "Erro";
                    summary.MensagemPrincipal = $"O cliente de comando terminou com código {exitCode.Value}.";
                }
                else
                {
                    summary.Estado = isTeste ? "Teste concluído" : "Resultado indeterminado";
                    summary.MensagemPrincipal = linhas.FirstOrDefault() ?? linhasErro.FirstOrDefault() ??
                        (exitSuccess ? "Execução terminada sem mensagens adicionais." : "Sem saída para interpretar.");

                    if (exitSuccess)
                    {
                        summary.Sucesso = true;
                    }
                }
            }

            return summary;

        }

        private static bool IsErroLinha(string linha)
        {
            if (string.IsNullOrWhiteSpace(linha))
            {
                return false;
            }

            // Ignore empty error tags or lines that become blank after stripping XML
            var cleaned = CleanOutputLine(linha);
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return false;
            }

            var texto = linha.Trim();
            if (!texto.Contains("erro", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var textoLower = texto.ToLowerInvariant();

            foreach (var indicador in NonErrorIndicators)
            {
                if (textoLower.Contains(indicador))
                {
                    return false;
                }
            }

            if (Regex.IsMatch(textoLower, @"erros?\s*[:=\-]\s*0"))
            {
                return false;
            }

            if (Regex.IsMatch(textoLower, @"0\s+erros?"))
            {
                return false;
            }

            return true;
        }

        private static bool IsClientUpdateLine(string linha)
        {
            if (string.IsNullOrWhiteSpace(linha))
            {
                return false;
            }

            if (ClientUpdateCodeRegex.IsMatch(linha))
            {
                return true;
            }

            foreach (var indicador in ClientUpdateIndicators)
            {
                if (linha.IndexOf(indicador, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string CleanOutputLine(string? linha)
        {
            if (string.IsNullOrWhiteSpace(linha))
            {
                return string.Empty;
            }

            var cleaned = XmlTagRegex.Replace(linha, string.Empty);
            return cleaned.Trim();
        }

        // Mantido por compatibilidade com builds que ainda referenciam o nome anterior.
        private static string CleanLine(string? linha) => CleanOutputLine(linha);
    }
}
