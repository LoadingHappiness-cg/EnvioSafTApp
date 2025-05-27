using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using EnvioSafTApp.Models;

namespace EnvioSafTApp.Services
{
    public static class HistoricoEnviosService
    {
        private static readonly string BaseFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EnviaSaft",
            "HistoricoEnvios");

        public static void RegistarEnvio(EnvioHistoricoEntry entry)
        {
            try
            {
                string empresa = SanearNome(entry.EmpresaNome ?? entry.NIF);
                string ano = entry.DataHora.Year.ToString();
                string mes = entry.DataHora.Month.ToString("D2");

                string pasta = Path.Combine(BaseFolder, empresa, ano);
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