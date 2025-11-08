using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using EnvioSafTApp.Models;

namespace EnvioSafTApp.Services
{
    public static class HistoricoEnviosService
    {
        private static readonly string _baseFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EnviaSaft",
            "HistoricoEnvios");

        public static string BaseFolder => _baseFolder;

        public static void RegistarEnvio(EnvioHistoricoEntry entry)
        {
            try
            {
                string empresa = SanearNome(entry.EmpresaNome ?? entry.NIF);
                string ano = entry.DataHora.Year.ToString();
                string mes = entry.DataHora.Month.ToString("D2");

                string pasta = Path.Combine(_baseFolder, empresa, ano);
                Directory.CreateDirectory(pasta); // garante criação recursiva

                string ficheiro = Path.Combine(pasta, $"{mes}.json");

                List<EnvioHistoricoEntry> lista;

                if (File.Exists(ficheiro))
                {
                    string conteudo = File.ReadAllText(ficheiro);
                    lista = JsonSerializer.Deserialize<List<EnvioHistoricoEntry>>(conteudo) ?? new List<EnvioHistoricoEntry>();
                }
                else
                {
                    lista = new List<EnvioHistoricoEntry>();
                }

                entry.Tags ??= new List<string>();
                lista.Add(entry);

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(ficheiro, JsonSerializer.Serialize(lista, options));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao gravar histórico: {ex.Message}");
                MessageBox.Show($"Erro ao gravar histórico: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static string GuardarLog(EnvioHistoricoEntry entry, string resumo, string output, string error)
        {
            try
            {
                string empresa = SanearNome(entry.EmpresaNome ?? entry.NIF);
                string ano = entry.DataHora.Year.ToString();
                string pasta = Path.Combine(_baseFolder, empresa, ano);
                Directory.CreateDirectory(pasta);

                string ficheiro = Path.Combine(pasta, $"{entry.DataHora:yyyyMMdd_HHmmss}_envio.log");

                var sb = new StringBuilder();
                sb.AppendLine($"Empresa: {entry.EmpresaNome}");
                sb.AppendLine($"NIF: {entry.NIF}");
                sb.AppendLine($"Operação: {entry.Operacao}");
                sb.AppendLine($"Resultado: {entry.Resultado}");
                sb.AppendLine($"Etiquetas: {(entry.Tags != null && entry.Tags.Any() ? string.Join(", ", entry.Tags) : "-")}");

                if (!string.IsNullOrWhiteSpace(resumo))
                {
                    sb.AppendLine();
                    sb.AppendLine("Resumo interpretado:");
                    sb.AppendLine(resumo);
                }

                if (!string.IsNullOrWhiteSpace(output))
                {
                    sb.AppendLine();
                    sb.AppendLine("Saída padrão:");
                    sb.AppendLine(output);
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    sb.AppendLine();
                    sb.AppendLine("Erros:");
                    sb.AppendLine(error);
                }

                File.WriteAllText(ficheiro, sb.ToString());
                return ficheiro;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static List<EnvioHistoricoEntry> ObterHistorico()
        {
            var lista = new List<EnvioHistoricoEntry>();

            if (!Directory.Exists(_baseFolder))
            {
                return lista;
            }

            foreach (var empresaDir in Directory.GetDirectories(_baseFolder))
            {
                string empresa = Path.GetFileName(empresaDir);

                foreach (var anoDir in Directory.GetDirectories(empresaDir))
                {
                    foreach (var ficheiro in Directory.GetFiles(anoDir, "*.json"))
                    {
                        try
                        {
                            string json = File.ReadAllText(ficheiro);
                            var entradas = JsonSerializer.Deserialize<List<EnvioHistoricoEntry>>(json);

                            if (entradas != null)
                            {
                                foreach (var entry in entradas)
                                {
                                    if (string.IsNullOrWhiteSpace(entry.EmpresaNome))
                                    {
                                        entry.EmpresaNome = empresa;
                                    }

                                    entry.Tags ??= new List<string>();

                                    if (entry.Ano == 0 && entry.DataHora.Year > 1900)
                                    {
                                        entry.Ano = entry.DataHora.Year;
                                    }

                                    if (string.IsNullOrWhiteSpace(entry.Mes) && entry.DataHora != default)
                                    {
                                        entry.Mes = entry.DataHora.Month.ToString("D2");
                                    }

                                    lista.Add(entry);
                                }
                            }
                        }
                        catch
                        {
                            // ignorar ficheiros corrompidos
                        }
                    }
                }
            }

            return lista;
        }

        public static void ExportarCsv(IEnumerable<EnvioHistoricoEntry> entradas, string destino)
        {
            var lista = entradas.ToList();
            if (!lista.Any())
            {
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Data,Empresa,NIF,Operacao,Resultado,Tags,FicheiroSaft,FicheiroOutput,Resumo");

            foreach (var entry in lista.OrderBy(e => e.DataHora))
            {
                string tags = entry.Tags != null && entry.Tags.Any() ? string.Join(";", entry.Tags) : string.Empty;
                sb.AppendLine(string.Join(",",
                    EscapeCsv(entry.DataHora.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
                    EscapeCsv(entry.EmpresaNome),
                    EscapeCsv(entry.NIF),
                    EscapeCsv(entry.Operacao),
                    EscapeCsv(entry.Resultado),
                    EscapeCsv(tags),
                    EscapeCsv(entry.FicheiroSaft),
                    EscapeCsv(entry.FicheiroOutput),
                    EscapeCsv(entry.Resumo ?? string.Empty)));
            }

            File.WriteAllText(destino, sb.ToString());
        }

        private static string EscapeCsv(string valor)
        {
            if (string.IsNullOrEmpty(valor))
            {
                return "";
            }

            if (valor.Contains('"') || valor.Contains(',') || valor.Contains('\n'))
            {
                valor = valor.Replace("\"", "\"\"");
                return $"\"{valor}\"";
            }

            return valor;
        }

        private static string SanearNome(string input)
        {
            // Remove ou substitui caracteres inválidos para nomes de pasta
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                input = input.Replace(c, '_');
            }

            return input.Replace("/", "_").Replace("\\", "_").Trim();
        }
    }
}
