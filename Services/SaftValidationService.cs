using EnvioSafTApp.Models;
using EnvioSafTApp.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;

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

        public SaftValidationService()
        {
            var baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EnviaSaft");
            _schemaFilePath = Path.Combine(baseFolder, "schemas", "SAFTPT1.04_01.xsd");
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

            var issues = new List<SaftValidationIssue>();
            var schemas = new XmlSchemaSet();
            bool schemaLoadFailed = false;

            try
            {
                if (!string.IsNullOrEmpty(schemaPath))
                {
                    try
                    {
                        schemas.Add(null, schemaPath);
                        schemas.Compile();
                    }
                    catch (XmlSchemaException schemaEx)
                    {
                        schemaLoadFailed = true;
                        resultado.Resumo = $"O ficheiro XSD está corrompido ou inválido: {schemaEx.Message}";
                        resultado.MensagemEstado = resultado.Resumo;
                        resultado.Sugestoes.Add("Volte a descarregar o ficheiro SAFTPT1.04_01.xsd a partir do portal da AT.");
                        resultado.Sugestoes.Add("Elimine o ficheiro inválido em: " + Path.GetDirectoryName(_schemaFilePath));
                        return resultado;
                    }
                }
            }
            catch (Exception ex)
            {
                resultado.Resumo = $"Falha ao carregar o XSD: {ex.Message}";
                resultado.MensagemEstado = resultado.Resumo;
                resultado.Sugestoes.Add("Volte a descarregar o ficheiro SAFTPT1.04_01.xsd a partir do portal da AT e repita a validação.");
                return resultado;
            }

            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                Schemas = schemas,
                ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings
                                  | XmlSchemaValidationFlags.ProcessSchemaLocation,
                ConformanceLevel = ConformanceLevel.Document
            };

            settings.ValidationEventHandler += (_, args) =>
            {
                var issue = new SaftValidationIssue
                {
                    Severidade = args.Severity == XmlSeverityType.Warning ? "Aviso" : "Erro",
                    Mensagem = ConstruirMensagemLegivel(args),
                    Linha = args.Exception?.LineNumber > 0 ? args.Exception?.LineNumber : null,
                    Coluna = args.Exception?.LinePosition > 0 ? args.Exception?.LinePosition : null,
                    Sugestao = CriarSugestao(args.Message)
                };

                issues.Add(issue);
            };

            try
            {
                using var stream = File.OpenRead(caminhoSafT);
                using var reader = XmlReader.Create(stream, settings);

                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                resultado.Resumo = $"Não foi possível concluir a validação: {ex.Message}";
                resultado.MensagemEstado = resultado.Resumo;
                resultado.Sugestoes.Add("Confirme se o ficheiro é um SAF-T válido e tente novamente.");
                return resultado;
            }

            resultado.Problemas = issues;
            resultado.Sucesso = issues.All(i => i.Severidade != "Erro");

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
                    if (IsValidXsdFile(_schemaFilePath))
                    {
                        return _schemaFilePath;
                    }
                    else
                    {
                        try
                        {
                            File.Delete(_schemaFilePath);
                        }
                        catch { }
                    }
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
                    if (bytes == null || bytes.Length == 0)
                    {
                        continue;
                    }

                    var folder = Path.GetDirectoryName(_schemaFilePath);
                    if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
                    {
                        Directory.CreateDirectory(folder);
                    }

                    await File.WriteAllBytesAsync(_schemaFilePath, bytes, cancellationToken);

                    if (IsValidXsdFile(_schemaFilePath))
                    {
                        return _schemaFilePath;
                    }
                    else
                    {
                        try
                        {
                            File.Delete(_schemaFilePath);
                        }
                        catch { }
                    }
                }
                catch
                {
                    continue;
                }
            }

            return null;
        }

        private static bool IsValidXsdFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                using (var stream = File.OpenRead(filePath))
                {
                    if (stream.Length == 0)
                        return false;

                    var doc = new XmlDocument();
                    doc.Load(stream);

                    var root = doc.DocumentElement;
                    if (root == null)
                        return false;

                    var isXsd = root.LocalName == "schema" &&
                               (root.NamespaceURI == "http://www.w3.org/2001/XMLSchema" ||
                                string.IsNullOrEmpty(root.NamespaceURI));

                    // If it's a valid XSD structure, return true even if compilation has warnings
                    return isXsd;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string ConstruirMensagemLegivel(ValidationEventArgs args)
        {
            var mensagem = Sanitize(args.Message);
            var linha = args.Exception?.LineNumber ?? 0;
            var coluna = args.Exception?.LinePosition ?? 0;

            if (linha > 0 || coluna > 0)
            {
                return $"Linha {linha}, Coluna {coluna}: {mensagem}";
            }

            return mensagem;
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
