using EnvioSafTApp.Models;
using EnvioSafTApp.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EnvioSafTApp.Services
{
    public class SaftValidationService : ISaftValidationService
    {
        private static readonly HttpClient HttpClient = new();
        private static readonly string[] XsdUrls = new[]
        {
            "https://info.portaldasfinancas.gov.pt/apps/saft-pt04/saftpt1.04_01.xsd"
        };

        private readonly string _schemaFilePath;
        private readonly string _validatorJarPath;

        public SaftValidationService()
        {
            var baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EnviaSaft");
            _schemaFilePath = Path.Combine(baseFolder, "schemas", "SAFTPT1.04_01.xsd");
            _validatorJarPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libs", "xsd11-validator.jar");
        }

        public async Task<SaftValidationResult> ValidateAsync(string caminhoSafT, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(caminhoSafT))
            {
                throw new ArgumentException("Caminho do ficheiro SAF-T inválido.", nameof(caminhoSafT));
            }

            var resultado = new SaftValidationResult
            {
                OrigemXsd = _schemaFilePath
            };

            if (!File.Exists(caminhoSafT))
            {
                resultado.Resumo = "O ficheiro SAF-T indicado não existe.";
                resultado.MensagemEstado = resultado.Resumo;
                resultado.Sugestoes.Add("Selecione um ficheiro SAF-T existente antes de validar.");
                return resultado;
            }

            string? schemaPath = await GarantirSchemaAsync(cancellationToken);
            resultado.EsquemaDisponivel = !string.IsNullOrWhiteSpace(schemaPath);
            resultado.OrigemXsd = schemaPath ?? _schemaFilePath;

            if (!resultado.EsquemaDisponivel)
            {
                resultado.Resumo = "Não foi possível obter o XSD oficial da AT (SAFTPT1.04_01.xsd).";
                resultado.MensagemEstado = resultado.Resumo;
                resultado.Sugestoes.Add($"Transfira manualmente o ficheiro SAFTPT1.04_01.xsd do portal da AT e coloque-o em: {Path.GetDirectoryName(_schemaFilePath)}.");
                resultado.Sugestoes.Add("Após colocar o XSD, execute novamente a validação.");
                return resultado;
            }

            if (!File.Exists(_validatorJarPath))
            {
                resultado.Resumo = "O ficheiro do validador XSD (xsd11-validator.jar) não foi encontrado.";
                resultado.MensagemEstado = resultado.Resumo;
                resultado.Sugestoes.Add($"Certifique-se de que o ficheiro 'xsd11-validator.jar' está localizado em: {Path.GetDirectoryName(_validatorJarPath)}");
                return resultado;
            }

            var issues = new List<SaftValidationIssue>();
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "java",
                        Arguments = $"-jar \"{_validatorJarPath}\" -sf \"{schemaPath}\" -if \"{caminhoSafT}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                var output = new StringBuilder();
                process.OutputDataReceived += (sender, args) => output.AppendLine(args.Data);
                process.ErrorDataReceived += (sender, args) => output.AppendLine(args.Data);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync(cancellationToken);

                var outputString = output.ToString();
                if (process.ExitCode != 0)
                {
                    var errorLines = outputString.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in errorLines)
                    {
                        if (line.StartsWith("Error:") || line.StartsWith("Exception in thread"))
                        {
                           issues.Add(ParseErrorLine(line));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                resultado.Resumo = $"Falha ao executar o validador XSD: {ex.Message}";
                resultado.MensagemEstado = resultado.Resumo;
                resultado.Sugestoes.Add("Certifique-se de que o Java está instalado e acessível a partir da linha de comandos.");
                return resultado;
            }

            resultado.Problemas = issues;
            resultado.Sucesso = !issues.Any();

            var erros = resultado.TotalErros;
            var avisos = resultado.TotalAvisos;

            if (resultado.Sucesso)
            {
                resultado.Resumo = "Ficheiro SAF-T compatível com o XSD oficial da AT.";
                resultado.MensagemEstado = "Validação concluída sem erros.";
                resultado.Sugestoes.Add("Sem erros estruturais. Pode prosseguir com o envio.");
            }
            else
            {
                var sb = new StringBuilder();
                sb.Append($"Foram detetados {erros} erro(s)");
                if (avisos > 0)
                {
                    sb.Append($" e {avisos} aviso(s)");
                }
                sb.Append(" no ficheiro SAF-T.");

                resultado.Resumo = sb.ToString();
                resultado.MensagemEstado = "Ajuste os erros identificados e revalide antes de enviar.";

                var sugestoes = issues
                    .Where(i => !string.IsNullOrWhiteSpace(i.Sugestao))
                    .Select(i => i.Sugestao!)
                    .Distinct()
                    .ToList();

                if (sugestoes.Any())
                {
                    resultado.Sugestoes.AddRange(sugestoes);
                }
                else
                {
                    resultado.Sugestoes.Add("Revise o campo indicado em cada erro para cumprir o esquema oficial.");
                }

                resultado.Sugestoes.Add("Depois de corrigir o ficheiro, execute novamente a validação.");
            }

            return resultado;
        }

        private SaftValidationIssue ParseErrorLine(string line)
        {
            var issue = new SaftValidationIssue { Severidade = "Erro", Mensagem = line };
            var match = Regex.Match(line, @"cvc-[^:]+:\s+(.*)\s+\[Line\s+(\d+),\s+Column\s+(\d+)\]");
            if (match.Success)
            {
                issue.Mensagem = Sanitize(match.Groups[1].Value);
                issue.Linha = int.Parse(match.Groups[2].Value);
                issue.Coluna = int.Parse(match.Groups[3].Value);
                issue.Sugestao = CriarSugestao(issue.Mensagem);
            }
            return issue;
        }

        private async Task<string?> GarantirSchemaAsync(CancellationToken cancellationToken)
        {
            try
            {
                var folder = Path.GetDirectoryName(_schemaFilePath);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                if (File.Exists(_schemaFilePath))
                {
                    return _schemaFilePath;
                }
            }
            catch
            {
                // Ignorar e tentar download
            }

            foreach (var url in XsdUrls)
            {
                try
                {
                    var bytes = await HttpClient.GetByteArrayAsync(url, cancellationToken);
                    await File.WriteAllBytesAsync(_schemaFilePath, bytes, cancellationToken);
                    return _schemaFilePath;
                }
                catch
                {
                    // tentar próximo URL
                    continue;
                }
            }

            return null;
        }

        private static string Sanitize(string? mensagem)
        {
            if (string.IsNullOrWhiteSpace(mensagem))
            {
                return string.Empty;
            }

            return mensagem.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static string CriarSugestao(string? mensagem)
        {
            if (string.IsNullOrWhiteSpace(mensagem))
            {
                return "Revise o campo indicado e alinhe-o com o esquema SAF-T.";
            }

            if (mensagem.Contains("TaxRegistrationNumber", StringComparison.OrdinalIgnoreCase))
            {
                return "Confirme o NIF em Header/TaxRegistrationNumber (9 dígitos numéricos).";
            }

            if (mensagem.Contains("InvoiceNo", StringComparison.OrdinalIgnoreCase) ||
                mensagem.Contains("DocumentNumber", StringComparison.OrdinalIgnoreCase))
            {
                return "Verifique o formato do número de documento (inclua série e separadores corretos).";
            }

            if (mensagem.Contains("DocumentStatus", StringComparison.OrdinalIgnoreCase))
            {
                return "Confirme se o estado do documento cumpre os valores permitidos no SAF-T.";
            }

            if (mensagem.Contains("TaxType", StringComparison.OrdinalIgnoreCase) ||
                mensagem.Contains("TaxCode", StringComparison.OrdinalIgnoreCase))
            {
                return "Valide se o tipo e código de imposto correspondem à tabela do SAF-T (IVA, IS, NS, etc.).";
            }

            if (mensagem.Contains("Amount", StringComparison.OrdinalIgnoreCase) ||
                mensagem.Contains("Total", StringComparison.OrdinalIgnoreCase))
            {
                return "Reveja totais e arredondamentos para garantir coerência com os valores declarados.";
            }

            if (mensagem.Contains("Date", StringComparison.OrdinalIgnoreCase))
            {
                return "Verifique o formato das datas (AAAA-MM-DD) e se estão dentro do período do ficheiro.";
            }

            return "Ajuste o elemento indicado de acordo com o XSD oficial e repita a validação.";
        }
    }
}
