using EnvioSafTApp.Models;
using EnvioSafTApp.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;

namespace EnvioSafTApp.Services
{
    public class SaftValidationService : ISaftValidationService
    {
        private static readonly string[] DefaultXsdUrls = new[]
        {
            "https://info.portaldasfinancas.gov.pt/apps/saft-pt04/saftpt1.04_01.xsd"
        };

        private static readonly Regex AssertElementRegex = new(
            @"<(?:[A-Za-z_][\w\-.]*:)?assert\b[^>]*(?:/>|>[\s\S]*?</(?:[A-Za-z_][\w\-.]*:)?assert>)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly string _schemaFilePath;
        private readonly HttpClient _httpClient;
        private readonly IReadOnlyList<string> _xsdUrls;

        public SaftValidationService()
            : this(schemaFilePath: null, xsdUrls: null, httpClient: null)
        {
        }

        public SaftValidationService(string? schemaFilePath, IEnumerable<string>? xsdUrls, HttpClient? httpClient)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EnviaSaft");
            _schemaFilePath = string.IsNullOrWhiteSpace(schemaFilePath)
                ? Path.Combine(baseFolder, "schemas", "SAFTPT1.04_01.xsd")
                : schemaFilePath;
            _httpClient = httpClient ?? new HttpClient();
            _xsdUrls = (xsdUrls ?? DefaultXsdUrls)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToArray();
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

            var issues = new List<SaftValidationIssue>();
            var schemas = new XmlSchemaSet();

            // Tentar carregar o schema se disponível
            if (!string.IsNullOrEmpty(schemaPath) && File.Exists(schemaPath))
            {
                try
                {
                    schemas.Add(null, schemaPath);
                    schemas.Compile();
                }
                catch
                {
                    // Se o schema falhar ao compilar, continuamos sem validação de schema
                    resultado.EsquemaDisponivel = false;
                }
            }

            var settings = new XmlReaderSettings
            {
                ValidationType = resultado.EsquemaDisponivel ? ValidationType.Schema : ValidationType.None,
                Schemas = schemas,
                ConformanceLevel = ConformanceLevel.Document,
                CheckCharacters = true
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

            if (resultado.Sucesso && !resultado.EsquemaDisponivel)
            {
                resultado.Resumo = "Ficheiro SAF-T é um XML válido. (Validação de schema não foi possível - ficheiro será enviado conforme)";
                resultado.MensagemEstado = "Aviso: Validação XSD indisponível. O ficheiro é um XML estruturalmente válido.";
                resultado.Sugestoes.Add("Recomenda-se validar contra o schema oficial antes de enviar para a AT.");
            }
            else if (resultado.Sucesso)
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
            var folder = Path.GetDirectoryName(_schemaFilePath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            if (File.Exists(_schemaFilePath))
            {
                try
                {
                    var fileInfo = new FileInfo(_schemaFilePath);
                    if (fileInfo.Length > 100)
                    {
                        if (await RemoverAssertDoSchemaAsync(_schemaFilePath, cancellationToken))
                        {
                            // Schema foi reescrito localmente sem asserts.
                        }

                        if (TryCompilarSchema(_schemaFilePath))
                        {
                            return _schemaFilePath;
                        }
                    }
                }
                catch
                {
                    // Ignorar e tentar download.
                }
            }

            foreach (var url in _xsdUrls)
            {
                try
                {
                    using var response = await _httpClient.GetAsync(url, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    if (bytes.Length == 0)
                    {
                        continue;
                    }

                    var schemaContent = DecodeSchema(bytes, out var schemaEncoding);
                    var stripped = AssertElementRegex.Replace(schemaContent, string.Empty);

                    if (string.IsNullOrWhiteSpace(stripped))
                    {
                        continue;
                    }

                    var targetEncoding = schemaEncoding ?? Encoding.UTF8;
                    var candidatePath = _schemaFilePath + ".download";
                    var candidateBytes = targetEncoding.GetBytes(stripped);

                    await File.WriteAllBytesAsync(candidatePath, candidateBytes, cancellationToken);

                    if (!TryCompilarSchema(candidatePath))
                    {
                        File.Delete(candidatePath);
                        continue;
                    }

                    File.Move(candidatePath, _schemaFilePath, true);
                    return _schemaFilePath;
                }
                catch
                {
                    // Ignorar erro deste endpoint e tentar o próximo.
                    continue;
                }
            }

            return null;
        }

        private static bool TryCompilarSchema(string schemaPath)
        {
            var schemas = new XmlSchemaSet();
            schemas.Add(null, schemaPath);
            schemas.Compile();
            return true;
        }

        private static string DecodeSchema(byte[] bytes, out Encoding? detectedEncoding)
        {
            detectedEncoding = DetectXmlEncoding(bytes);
            if (detectedEncoding != null)
            {
                return detectedEncoding.GetString(bytes);
            }

            try
            {
                detectedEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
                return detectedEncoding.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                detectedEncoding = Encoding.GetEncoding("Windows-1252");
                return detectedEncoding.GetString(bytes);
            }
        }

        private static Encoding? DetectXmlEncoding(byte[] bytes)
        {
            var headerLength = Math.Min(bytes.Length, 512);
            if (headerLength <= 0)
            {
                return null;
            }

            var header = Encoding.ASCII.GetString(bytes, 0, headerLength);
            var match = Regex.Match(header, @"encoding\s*=\s*[""'](?<enc>[^""']+)[""']", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            var encodingName = match.Groups["enc"].Value;
            if (string.IsNullOrWhiteSpace(encodingName))
            {
                return null;
            }

            try
            {
                return Encoding.GetEncoding(encodingName);
            }
            catch
            {
                return null;
            }
        }

        private static async Task<bool> RemoverAssertDoSchemaAsync(string schemaPath, CancellationToken cancellationToken)
        {
            byte[] bytes;
            try
            {
                bytes = await File.ReadAllBytesAsync(schemaPath, cancellationToken);
            }
            catch
            {
                return false;
            }

            var content = DecodeSchema(bytes, out var encoding);
            var stripped = AssertElementRegex.Replace(content, string.Empty);
            if (string.Equals(content, stripped, StringComparison.Ordinal))
            {
                return false;
            }

            var targetEncoding = encoding ?? Encoding.UTF8;
            await File.WriteAllBytesAsync(schemaPath, targetEncoding.GetBytes(stripped), cancellationToken);
            return true;
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
