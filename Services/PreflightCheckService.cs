using EnvioSafTApp.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnvioSafTApp.Services
{
    public static class PreflightCheckService
    {
        private static readonly string[] JavaCommands = new[] { "java", "java.exe" };

        public static async Task<IReadOnlyList<PreflightCheckResult>> RunAsync()
        {
            var checks = new List<PreflightCheckResult>
            {
                await VerificarJavaAsync(),
                await Task.Run(VerificarJar),
                await Task.Run(VerificarPermissoesEscrita)
            };

            return checks;
        }

        private static async Task<PreflightCheckResult> VerificarJavaAsync()
        {
            var resultado = new PreflightCheckResult
            {
                Nome = "Java Runtime"
            };

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = JavaCommands.First(),
                    Arguments = "-version",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var processo = Process.Start(psi);
                if (processo == null)
                {
                    throw new InvalidOperationException("Não foi possível iniciar o processo do Java.");
                }

                string stdout = await processo.StandardOutput.ReadToEndAsync();
                string stderr = await processo.StandardError.ReadToEndAsync();
                await processo.WaitForExitAsync();

                if (processo.ExitCode == 0)
                {
                    resultado.Sucesso = true;
                    resultado.Detalhes = !string.IsNullOrWhiteSpace(stdout) ? stdout.Trim() : stderr.Trim();
                }
                else
                {
                    resultado.Sucesso = false;
                    resultado.Detalhes = string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : stderr.Trim();
                    resultado.ResolucaoSugerida = "Instale o Java Runtime Environment (JRE) 8 ou superior e garanta que o comando 'java' está disponível no PATH.";
                }
            }
            catch (Exception ex)
            {
                resultado.Sucesso = false;
                resultado.Detalhes = ex.Message;
                resultado.ResolucaoSugerida = "Certifique-se de que o Java está instalado e configurado no PATH do sistema.";
            }

            return resultado;
        }

        private static PreflightCheckResult VerificarJar()
        {
            string jarPath = JarUpdateService.GetLocalJarPath();
            bool existe = File.Exists(jarPath);
            string fileName = Path.GetFileName(jarPath);

            return new PreflightCheckResult
            {
                Nome = string.IsNullOrWhiteSpace(fileName) ? "Aplicação de envio da AT" : fileName,
                Sucesso = existe,
                Detalhes = existe
                    ? $"Encontrado em {jarPath}"
                    : "O ficheiro da aplicação oficial da AT não foi encontrado na pasta 'libs'.",
                ResolucaoSugerida = existe
                    ? string.Empty
                    : "Copie o ficheiro .jar fornecido pela AT para a pasta 'libs' da aplicação (sem necessidade de renomear)."
            };
        }

        private static PreflightCheckResult VerificarPermissoesEscrita()
        {
            string pastaHistorico = HistoricoEnviosService.BaseFolder;
            string pastaTemp = Path.Combine(Path.GetTempPath(), "EnviaSaftAppTest");

            var sb = new StringBuilder();
            bool sucesso = true;

            try
            {
                Directory.CreateDirectory(pastaHistorico);
                string ficheiroTeste = Path.Combine(pastaHistorico, $"permissao_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(ficheiroTeste, "teste");
                File.Delete(ficheiroTeste);
                sb.AppendLine($"Permissões OK para {pastaHistorico}");
            }
            catch (Exception ex)
            {
                sucesso = false;
                sb.AppendLine($"Sem permissões de escrita em {pastaHistorico}: {ex.Message}");
            }

            try
            {
                Directory.CreateDirectory(pastaTemp);
                Directory.Delete(pastaTemp, true);
                sb.AppendLine("Pasta temporária disponível.");
            }
            catch (Exception ex)
            {
                sucesso = false;
                sb.AppendLine($"Problema ao aceder à pasta temporária: {ex.Message}");
            }

            return new PreflightCheckResult
            {
                Nome = "Permissões e armazenamento",
                Sucesso = sucesso,
                Detalhes = sb.ToString().Trim(),
                ResolucaoSugerida = sucesso ? string.Empty : "Execute a aplicação com permissões elevadas ou escolha diretórios com acesso de leitura/escrita."
            };
        }
    }
}
