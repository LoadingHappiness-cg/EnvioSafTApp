using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnvioSafTApp.Models;
using EnvioSafTApp.Services;
using EnvioSafTApp.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace EnvioSafTApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IJarUpdateService _jarUpdateService;
        private readonly IPreflightCheckService _preflightCheckService;
        private readonly IHistoricoEnviosService _historicoEnviosService;

        [ObservableProperty]
        private string _appVersion = "1.0.0";

        [ObservableProperty]
        private ObservableCollection<PreflightCheckResult> _preflightChecks = new();

        [ObservableProperty]
        private string _preflightStatusText = "A aguardar...";

        [ObservableProperty]
        private bool _isPreflightRunning;

        [ObservableProperty]
        private bool _isPreflightSuccess;

        // Ticker Properties
        [ObservableProperty]
        private string _tickerMessage = "";

        [ObservableProperty]
        private TickerMessageType _tickerType = TickerMessageType.Info;

        // Output Properties
        [ObservableProperty]
        private string _outputText = "";

        [ObservableProperty]
        private string _outputSummary = "";

        [ObservableProperty]
        private string _ultimoLogPath = "";

        // History Properties
        [ObservableProperty]
        private ObservableCollection<EnvioHistoricoEntry> _historicoFiltrado = new();

        [ObservableProperty]
        private ObservableCollection<string> _empresas = new();

        [ObservableProperty]
        private ObservableCollection<string> _operacoesHistorico = new();

        [ObservableProperty]
        private ObservableCollection<string> _resultadosHistorico = new();

        [ObservableProperty]
        private string _historicoEstatisticas = "";

        // History Filters
        [ObservableProperty]
        private string? _filtroEmpresa;
        partial void OnFiltroEmpresaChanged(string? value) => AplicarFiltrosHistorico();

        [ObservableProperty]
        private string? _filtroOperacao;
        partial void OnFiltroOperacaoChanged(string? value) => AplicarFiltrosHistorico();

        [ObservableProperty]
        private string? _filtroResultado;
        partial void OnFiltroResultadoChanged(string? value) => AplicarFiltrosHistorico();

        [ObservableProperty]
        private DateTime? _filtroInicio;
        partial void OnFiltroInicioChanged(DateTime? value) => AplicarFiltrosHistorico();

        [ObservableProperty]
        private DateTime? _filtroFim;
        partial void OnFiltroFimChanged(DateTime? value) => AplicarFiltrosHistorico();

        [ObservableProperty]
        private string _filtroEtiquetas = "";
        partial void OnFiltroEtiquetasChanged(string value) => AplicarFiltrosHistorico();

        private List<EnvioHistoricoEntry> _historicoCompleto = new();
        private bool _historicoInicializado;

        // Form Properties
        [ObservableProperty]
        private string _nomeEmpresa = "Desconhecida";

        [ObservableProperty]
        private string _ano = DateTime.Now.Year.ToString();

        [ObservableProperty]
        private string _mes = DateTime.Now.Month.ToString("D2");

        [ObservableProperty]
        private string _operacao = "enviar";

        [ObservableProperty]
        private string _nif = "";

        [ObservableProperty]
        private string _password = "";

        [ObservableProperty]
        private string _ficheiroSafT = "";

        [ObservableProperty]
        private bool _isTeste;

        [ObservableProperty]
        private bool _isAutoFaturacao;

        [ObservableProperty]
        private string _arquivoPassword = "";

        [ObservableProperty]
        private string _nifEmitente = "";

        [ObservableProperty]
        private string _outputFile = "";

        [ObservableProperty]
        private string _memoria = "";

        [ObservableProperty]
        private string _etiquetas = "";

        private readonly IFileService _fileService;
        private string? _pastaTemporaria;

        public MainViewModel(
            IJarUpdateService jarUpdateService,
            IPreflightCheckService preflightCheckService,
            IHistoricoEnviosService historicoEnviosService,
            IFileService fileService)
        {
            _jarUpdateService = jarUpdateService;
            _preflightCheckService = preflightCheckService;
            _historicoEnviosService = historicoEnviosService;
            _fileService = fileService;
            
            AppVersion = $"v{GetVersion()} – loadinghappiness.pt";
        }

        [RelayCommand]
        private async Task BrowseFileAsync()
        {
            var filePath = _fileService.OpenFileDialog("Ficheiros SAF-T (*.xml, *.zip, *.gz, *.tar, *.rar)|*.xml;*.zip;*.gz;*.tar;*.rar");
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                try
                {
                    LimparPastaTemporaria();
                    var progress = new Progress<FileExtractionProgress>(p =>
                    {
                        if (!string.IsNullOrWhiteSpace(p.Mensagem))
                        {
                            ShowTicker(p.Mensagem, TickerMessageType.Info);
                        }
                    });

                    var result = await Task.Run(() =>
                        FileExtractionHelper.ObterXmlDoFicheiro(filePath,
                            string.IsNullOrWhiteSpace(ArquivoPassword) ? null : ArquivoPassword,
                            progress));

                    FicheiroSafT = result.caminhoXml;
                    _pastaTemporaria = result.pastaTemp;
                    
                    PreencherCamposDoFicheiroSafT(result.caminhoXml);
                    
                    ShowTicker("Ficheiro SAF-T carregado com sucesso.", TickerMessageType.Success);
                }
                catch (Exception ex)
                {
                    ShowTicker($"Erro ao extrair ficheiro: {ex.Message}", TickerMessageType.Error);
                }
            }
        }

        [RelayCommand]
        private void BrowseOutputFile()
        {
            var filePath = _fileService.SaveFileDialog("Ficheiro XML (*.xml)|*.xml", ".xml");
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                OutputFile = filePath;
                ShowTicker("Destino do ficheiro definido com sucesso.", TickerMessageType.Success);
            }
        }

        private void ShowTicker(string message, TickerMessageType type)
        {
            TickerMessage = message;
            TickerType = type;
        }

        private void LimparPastaTemporaria()
        {
            if (string.IsNullOrWhiteSpace(_pastaTemporaria))
            {
                return;
            }

            try
            {
                if (Directory.Exists(_pastaTemporaria))
                {
                    Directory.Delete(_pastaTemporaria, true);
                }
            }
            catch
            {
                // Ignorar falhas de limpeza
            }
            finally
            {
                _pastaTemporaria = null;
            }
        }

        private void PreencherCamposDoFicheiroSafT(string caminho)
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                string detectedEncoding = "utf-8";

                using (var fs = new FileStream(caminho, FileMode.Open, FileAccess.Read))
                using (var sr = new StreamReader(fs, Encoding.Default, true))
                {
                    char[] buffer = new char[512];
                    sr.Read(buffer, 0, buffer.Length);
                    string header = new string(buffer);
                    var match = Regex.Match(header, @"encoding=['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
                    if (match.Success)
                        detectedEncoding = match.Groups[1].Value;
                }

                var doc = new XmlDocument();
                using var reader = new StreamReader(caminho, Encoding.GetEncoding(detectedEncoding));
                doc.Load(reader);

                var nsMgr = new XmlNamespaceManager(doc.NameTable);
                nsMgr.AddNamespace("pt", "urn:OECD:StandardAuditFile-Tax:PT_1.04_01");

                var nifNode = doc.SelectSingleNode("//pt:Header/pt:TaxRegistrationNumber", nsMgr);
                if (nifNode != null)
                    Nif = nifNode.InnerText;

                // NomeEmpresa logic - we need a property for this?
                // var nameNode = doc.SelectSingleNode("//pt:Header/pt:BusinessName", nsMgr);
                // if (nameNode != null)
                //     _nomeEmpresa = nameNode.InnerText.Trim();

                var dataNode = doc.SelectSingleNode("//pt:SourceDocuments//pt:Invoice/pt:InvoiceDate", nsMgr) ??
                               doc.SelectSingleNode("//pt:SourceDocuments//pt:Transaction/pt:TransactionDate", nsMgr);

                if (dataNode != null && DateTime.TryParse(dataNode.InnerText, out var data))
                {
                    Ano = data.Year.ToString();
                    Mes = data.Month.ToString("D2");
                }
            }
            catch (Exception ex)
            {
                ShowTicker($"Erro ao ler ficheiro SAF-T: {ex.Message}", TickerMessageType.Warning);
            }
        }

        [RelayCommand]
        private async Task EnviarAsync()
        {
            if (!ValidarCamposObrigatorios())
                return;

            if (!File.Exists(FicheiroSafT))
            {
                ShowTicker("Ficheiro SAF-T não encontrado.", TickerMessageType.Error);
                return;
            }

            var jarUpdateResult = await _jarUpdateService.EnsureLatestAsync(CancellationToken.None);
            string jarFileName = Path.GetFileName(jarUpdateResult.JarPath);

            if (!jarUpdateResult.Success)
            {
                var message = jarUpdateResult.Message ?? $"Não foi possível preparar o {jarFileName}.";
                ShowTicker(message, jarUpdateResult.UsedFallback ? TickerMessageType.Warning : TickerMessageType.Error);

                if (!jarUpdateResult.UsedFallback)
                {
                    return;
                }
            }
            else
            {
                if (jarUpdateResult.Updated)
                {
                    ShowTicker(jarUpdateResult.Message ?? $"Foi descarregada a versão mais recente do {jarFileName}.", TickerMessageType.Info);
                }
                else if (jarUpdateResult.UsedFallback)
                {
                    var message = jarUpdateResult.Message ?? $"Não foi possível confirmar atualizações do {jarFileName}; a versão local será utilizada.";
                    ShowTicker(message, TickerMessageType.Warning);
                }
            }

            string jarPath = jarUpdateResult.JarPath;
            string updatePath = Path.GetDirectoryName(jarPath) ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libs");

            if (!File.Exists(jarPath))
            {
                ShowTicker($"O ficheiro {jarFileName} não está disponível após a tentativa de atualização.", TickerMessageType.Error);
                return;
            }

            OutputText = "";
            OutputSummary = string.Empty;
            // _ultimoLogPath = null; // Need property?
            
            // TODO: Switch tab to Result (Need a way to control tabs from VM or use a service/message)
            
            try
            {
                var dataEnvio = DateTime.Now;
                var resultado = await Task.Run(() =>
                {
                    var psi = CriarProcessoEnvio(jarPath, Nif, Password, Ano, Mes, Operacao, FicheiroSafT, updatePath, Memoria, IsTeste, IsAutoFaturacao, NifEmitente, OutputFile);

                    using var proc = Process.Start(psi);
                    if (proc == null)
                        return ("", "Falha ao iniciar o processo Java.");

                    string stdout = proc.StandardOutput.ReadToEnd();
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();

                    return (stdout, stderr);
                });

                string output = resultado.Item1;
                string error = resultado.Item2;

                var resumo = AtResponseInterpreter.Interpret(output, error);
                // _ultimoResumo = resumo; // Need property?
                string resumoLegivel = resumo.ConstruirResumoLegivel();
                OutputSummary = string.IsNullOrWhiteSpace(resumoLegivel)
                    ? "Sem informação interpretável."
                    : resumoLegivel;

                var blocos = new List<string>();
                if (!string.IsNullOrWhiteSpace(output)) blocos.Add(output.Trim());
                if (!string.IsNullOrWhiteSpace(error)) blocos.Add(error.Trim());
                OutputText = string.Join(Environment.NewLine + Environment.NewLine, blocos);

                string? savedJarFileName = null;
                bool jarPersisted = false;
                if (resumo.RequerAtualizacaoCliente)
                {
                    string? updatedJarPath = JarUpdateService.ExtractJarPathFromOutput(output) ?? jarPath;
                    var persistenceResult = await _jarUpdateService.RememberJarAsync(updatedJarPath, CancellationToken.None);
                    if (!string.IsNullOrWhiteSpace(persistenceResult.SavedPath))
                    {
                        savedJarFileName = Path.GetFileName(persistenceResult.SavedPath);
                        jarPersisted = persistenceResult.IsNew;
                    }
                }

                if (jarPersisted && !string.IsNullOrWhiteSpace(savedJarFileName))
                {
                    var notaAtualizacao = $"Novo ficheiro do cliente guardado: {savedJarFileName}.";
                    if (string.IsNullOrWhiteSpace(OutputSummary) || OutputSummary == "Sem informação interpretável.")
                    {
                        OutputSummary = notaAtualizacao;
                    }
                    else
                    {
                        OutputSummary += Environment.NewLine + Environment.NewLine + notaAtualizacao;
                    }
                }

                bool sucesso = resumo.Sucesso;
                string resultadoFinal = IsTeste ? "teste" : resumo.RequerAtualizacaoCliente ? "atualizacao" : sucesso ? "sucesso" : "erro";

                var tickerType = resumo.RequerAtualizacaoCliente
                    ? TickerMessageType.Warning
                    : sucesso
                        ? TickerMessageType.Success
                        : TickerMessageType.Error;

                string tickerMessage;
                if (resumo.RequerAtualizacaoCliente)
                {
                    tickerMessage = !string.IsNullOrWhiteSpace(resumo.MensagemPrincipal)
                        ? resumo.MensagemPrincipal
                        : "A AT solicitou a atualização do cliente de comando. Volte a tentar após a atualização.";

                    if (jarPersisted && !string.IsNullOrWhiteSpace(savedJarFileName))
                    {
                        tickerMessage += $" Novo ficheiro guardado: {savedJarFileName}.";
                    }
                }
                else if (sucesso)
                {
                    tickerMessage = "Envio realizado com sucesso.";
                }
                else
                {
                    tickerMessage = !string.IsNullOrWhiteSpace(resumo.MensagemPrincipal)
                        ? resumo.MensagemPrincipal
                        : "Erro ao enviar ficheiro.";
                }

                ShowTicker(tickerMessage, tickerType);

                // Guardar no histórico
                var entrada = new EnvioHistoricoEntry
                {
                    EmpresaNome = NomeEmpresa,
                    NIF = Nif,
                    DataHora = dataEnvio,
                    Ano = int.TryParse(Ano, out var anoInt) ? anoInt : DateTime.Now.Year,
                    Mes = Mes,
                    FicheiroSaft = FicheiroSafT,
                    FicheiroOutput = OutputFile,
                    Operacao = Operacao,
                    Resultado = resultadoFinal,
                    Resumo = resumoLegivel,
                    Tags = Etiquetas.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };

                entrada.LogFilePath = _historicoEnviosService.GuardarLog(entrada, resumoLegivel, output, error);
                UltimoLogPath = entrada.LogFilePath;
                _historicoEnviosService.RegistarEnvio(entrada);

                if (_historicoInicializado)
                {
                    _historicoCompleto.Add(entrada);
                    AplicarFiltrosHistorico();
                }
            }
            catch (Exception ex)
            {
                ShowTicker($"Erro ao executar o envio: {ex.Message}", TickerMessageType.Error);
            }
        }

        private bool ValidarCamposObrigatorios()
        {
            if (string.IsNullOrWhiteSpace(Ano)) { ShowTicker("O ano é obrigatório.", TickerMessageType.Error); return false; }
            if (string.IsNullOrWhiteSpace(Mes)) { ShowTicker("O mês é obrigatório.", TickerMessageType.Error); return false; }
            if (string.IsNullOrWhiteSpace(Nif)) { ShowTicker("O NIF é obrigatório.", TickerMessageType.Error); return false; }
            if (string.IsNullOrWhiteSpace(Password)) { ShowTicker("A password é obrigatória.", TickerMessageType.Error); return false; }
            if (string.IsNullOrWhiteSpace(FicheiroSafT)) { ShowTicker("O ficheiro SAF-T é obrigatório.", TickerMessageType.Error); return false; }
            return true;
        }

        private ProcessStartInfo CriarProcessoEnvio(
            string jarPath,
            string nif,
            string password,
            string ano,
            string mes,
            string op,
            string ficheiro,
            string updatePath,
            string memoria,
            bool isTeste,
            bool isAf,
            string nifEmitente,
            string outputPath)
        {
            var psi = new ProcessStartInfo("java")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (!string.IsNullOrWhiteSpace(memoria))
            {
                foreach (var argumentoMemoria in memoria
                             .Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    psi.ArgumentList.Add(argumentoMemoria);
                }
            }

            psi.ArgumentList.Add("-jar");
            psi.ArgumentList.Add(jarPath);
            psi.ArgumentList.Add("-n");
            psi.ArgumentList.Add(nif);
            psi.ArgumentList.Add("-p");
            psi.ArgumentList.Add(password);
            psi.ArgumentList.Add("-a");
            psi.ArgumentList.Add(ano);
            psi.ArgumentList.Add("-m");
            psi.ArgumentList.Add(mes);
            psi.ArgumentList.Add("-op");
            psi.ArgumentList.Add(op);
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(ficheiro);
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(updatePath);

            if (isTeste)
            {
                psi.ArgumentList.Add("-t");
            }

            if (isAf)
            {
                psi.ArgumentList.Add("-af");
            }

            if (!string.IsNullOrWhiteSpace(nifEmitente))
            {
                psi.ArgumentList.Add("-ea");
                psi.ArgumentList.Add(nifEmitente);
            }

            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                psi.ArgumentList.Add("-o");
                psi.ArgumentList.Add(outputPath);
            }

            return psi;
        }

        [RelayCommand]
        private void CopiarResultado()
        {
            if (!string.IsNullOrWhiteSpace(OutputText))
            {
                // Clipboard is UI thread specific. 
                // In pure MVVM, we should use a service.
                // But for now, we can use System.Windows.Clipboard if we are in WPF project.
                // Or pass it as a service.
                // Since this is a WPF app, referencing PresentationCore is fine.
                System.Windows.Clipboard.SetText(OutputText);
                ShowTicker("Resultado copiado para a área de transferência.", TickerMessageType.Success);
            }
        }

        [RelayCommand]
        private void AbrirUltimoLog()
        {
            if (!string.IsNullOrWhiteSpace(UltimoLogPath) && File.Exists(UltimoLogPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = UltimoLogPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    ShowTicker($"Erro ao abrir log: {ex.Message}", TickerMessageType.Error);
                }
            }
            else
            {
                ShowTicker("Nenhum log disponível para abrir.", TickerMessageType.Warning);
            }
        }

        [RelayCommand]
        private void LimparResultado()
        {
            OutputText = "";
            OutputSummary = "";
            UltimoLogPath = "";
            ShowTicker("Painel de resultados limpo.", TickerMessageType.Info);
        }

        public void CarregarHistorico()
        {
            if (!_historicoInicializado)
            {
                _historicoCompleto.Clear();
                var lista = _historicoEnviosService.ObterHistorico();
                _historicoCompleto.AddRange(lista);
                AtualizarOpcoesFiltrosHistorico();
                _historicoInicializado = true;
            }

            AplicarFiltrosHistorico();
        }

        private void AtualizarOpcoesFiltrosHistorico()
        {
            Empresas = new ObservableCollection<string>(_historicoCompleto
                .Select(e => e.EmpresaNome)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(e => e));

            OperacoesHistorico = new ObservableCollection<string>(_historicoCompleto
                .Select(e => e.Operacao)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(e => e));

            ResultadosHistorico = new ObservableCollection<string>(_historicoCompleto
                .Select(e => e.Resultado)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(e => e));
        }

        private void AplicarFiltrosHistorico()
        {
            IEnumerable<EnvioHistoricoEntry> query = _historicoCompleto;

            if (!string.IsNullOrWhiteSpace(FiltroEmpresa))
            {
                query = query.Where(e => string.Equals(e.EmpresaNome, FiltroEmpresa, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(FiltroOperacao))
            {
                query = query.Where(e => string.Equals(e.Operacao, FiltroOperacao, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(FiltroResultado))
            {
                query = query.Where(e => string.Equals(e.Resultado, FiltroResultado, StringComparison.OrdinalIgnoreCase));
            }

            if (FiltroInicio is DateTime inicio)
            {
                query = query.Where(e => e.DataHora >= inicio.Date);
            }

            if (FiltroFim is DateTime fim)
            {
                var fimInclusivo = fim.Date.AddDays(1).AddTicks(-1);
                query = query.Where(e => e.DataHora <= fimInclusivo);
            }

            var etiquetasFiltro = FiltroEtiquetas?
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            if (etiquetasFiltro.Any())
            {
                query = query.Where(e => e.Tags != null && etiquetasFiltro.All(tag => e.Tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase))));
            }

            var filtrado = query.OrderByDescending(e => e.DataHora).ToList();
            HistoricoFiltrado = new ObservableCollection<EnvioHistoricoEntry>(filtrado);
            HistoricoEstatisticas = GerarEstatisticasHistorico(filtrado);
        }

        private string GerarEstatisticasHistorico(List<EnvioHistoricoEntry> lista)
        {
            if (!lista.Any())
            {
                return "Sem registos para apresentar.";
            }

            int total = lista.Count;
            int sucesso = lista.Count(e => string.Equals(e.Resultado, "sucesso", StringComparison.OrdinalIgnoreCase));
            int erro = lista.Count(e => string.Equals(e.Resultado, "erro", StringComparison.OrdinalIgnoreCase));
            int teste = lista.Count(e => string.Equals(e.Resultado, "teste", StringComparison.OrdinalIgnoreCase));
            int atualizacao = lista.Count(e => string.Equals(e.Resultado, "atualizacao", StringComparison.OrdinalIgnoreCase));

            var sb = new StringBuilder();
            sb.AppendLine($"Total: {total} | Sucesso: {sucesso} | Erros: {erro} | Testes: {teste} | Atualizações: {atualizacao}");
            sb.AppendLine($"Taxa de sucesso global: {(total > 0 ? sucesso * 100.0 / total : 0):F1}%");
            return sb.ToString();
        }

        [RelayCommand]
        private void ExportarHistorico()
        {
            if (!HistoricoFiltrado.Any())
            {
                ShowTicker("Não há dados para exportar.", TickerMessageType.Warning);
                return;
            }

            var filePath = _fileService.SaveFileDialog("Ficheiro CSV (*.csv)|*.csv", ".csv");
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                try
                {
                    _historicoEnviosService.ExportarCsv(HistoricoFiltrado, filePath);
                    ShowTicker("Histórico exportado com sucesso.", TickerMessageType.Success);
                }
                catch (Exception ex)
                {
                    ShowTicker($"Erro ao exportar histórico: {ex.Message}", TickerMessageType.Error);
                }
            }
        }

        [RelayCommand]
        private void LimparFiltrosHistorico()
        {
            FiltroEmpresa = null;
            FiltroOperacao = null;
            FiltroResultado = null;
            FiltroInicio = null;
            FiltroFim = null;
            FiltroEtiquetas = "";
            ShowTicker("Filtros limpos.", TickerMessageType.Info);
        }

        [RelayCommand]
        private void OpenSite()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.loadinghappiness.pt",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowTicker($"Erro ao abrir site: {ex.Message}", TickerMessageType.Error);
            }
        }

        [RelayCommand]
        private async Task RunPreflightCheckAsync()
        {
            try
            {
                IsPreflightRunning = true;
                PreflightStatusText = "A executar verificações...";
                PreflightChecks.Clear();

                var resultados = await _preflightCheckService.RunAsync();
                foreach (var resultado in resultados)
                {
                    PreflightChecks.Add(resultado);
                }

                bool tudoOk = resultados.All(r => r.Sucesso);
                IsPreflightSuccess = tudoOk;
                PreflightStatusText = tudoOk
                    ? "Ambiente pronto para envio."
                    : "Algumas verificações requerem atenção. Veja as instruções abaixo.";
            }
            catch (Exception ex)
            {
                PreflightStatusText = $"Falha ao executar pré-validação: {ex.Message}";
            }
            finally
            {
                IsPreflightRunning = false;
            }
        }


        private string GetVersion()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? version.ToString(3) : "1.0.0";
        }
    }
}
