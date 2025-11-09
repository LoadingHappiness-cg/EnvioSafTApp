using EnvioSafTApp.Models;
using EnvioSafTApp.Services;
using Microsoft.Win32;
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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Xml;

namespace EnvioSafTApp
{
    public partial class MainWindow : Window
    {
        private readonly StatusTickerService _ticker;
        private readonly ObservableCollection<PreflightCheckResult> _preflightChecks = new();
        private readonly List<EnvioHistoricoEntry> _historicoCompleto = new();
        private List<EnvioHistoricoEntry> _historicoFiltrado = new();
        private string _nomeEmpresa = "Desconhecida";
        private string? _pastaTemporaria;
        private AtResponseSummary? _ultimoResumo;
        private bool _historicoInicializado;

        public MainWindow()
        {
            InitializeComponent();
            AppVersionTextBlock.Text = $"v{ObterVersao()} – loadinghappiness.pt";
            _ticker = new StatusTickerService(StatusTicker, StatusTickerIcon, StatusTickerBorder);
            PreflightListView.ItemsSource = _preflightChecks;
            MostrarAjudaInicial();
            MostrarAnimacaoCabecalhoAjuda();
            MostrarLogoComAnimacao();
            this.PreviewMouseDown += Window_PreviewMouseDown;
            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await ExecutarPreValidacaoAsync(false);
        }

        private string ObterVersao()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? version.ToString(3) : "1.0.0";
        }

        private void MostrarAjudaInicial()
        {
            HelpList.ItemsSource = new List<HelpEntry>
            {
                new HelpEntry
                {
                    Campo = "Aguardando ficheiro SAF-T...",
                    Descricao = "Por favor, escolha primeiro o ficheiro SAF-T para começar. As instruções contextuais aparecerão aqui."
                }
            };
        }

        private void AtualizarAjuda(string campo)
        {
            var entry = HelpEntry.GetDefaultEntries().Find(h => h.Campo == campo);
            if (entry != null)
            {
                HelpList.ItemsSource = new List<HelpEntry> { entry };
                MostrarAnimacaoCabecalhoAjuda(); // Opcional, se quiseres animação cada vez que muda
            }
        }

        private async Task ExecutarPreValidacaoAsync(bool mostrarTicker = true)
        {
            try
            {
                PreflightProgressBar.Visibility = Visibility.Visible;
                PreflightStatusTextBlock.Text = "A executar verificações...";
                _preflightChecks.Clear();

                var resultados = await PreflightCheckService.RunAsync();
                foreach (var resultado in resultados)
                {
                    _preflightChecks.Add(resultado);
                }

                bool tudoOk = resultados.All(r => r.Sucesso);
                PreflightStatusTextBlock.Text = tudoOk
                    ? "Ambiente pronto para envio."
                    : "Algumas verificações requerem atenção. Veja as instruções abaixo.";

                if (mostrarTicker)
                {
                    _ticker.ShowMessage(
                        tudoOk ? "Pré-validação concluída com sucesso." : "Pré-validação encontrou problemas.",
                        tudoOk ? TickerMessageType.Success : TickerMessageType.Warning);
                }
            }
            catch (Exception ex)
            {
                PreflightStatusTextBlock.Text = $"Falha ao executar pré-validação: {ex.Message}";
                _ticker.ShowMessage($"Falha na pré-validação: {ex.Message}", TickerMessageType.Error);
            }
            finally
            {
                PreflightProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private string GetCampoAjuda(string nomeCampo)
        {
            return nomeCampo switch
            {
                "AnoTextBox" or "MesTextBox" => "Ano / Mês",
                "NifTextBox" or "PasswordBox" => "NIF / Password",
                "OperacaoComboBox" => "Operação",
                "FicheiroTextBox" => "Ficheiro SAF-T",
                "ArquivoPasswordBox" => "Password do Arquivo",
                "NifEmitenteTextBox" => "NIF Emitente",
                "OutputTextBox" => "Ficheiro de Retorno",
                "MemoriaTextBox" => "Memória",
                "EtiquetasTextBox" => "Etiquetas",
                "AutoFaturacaoCheckBox" => "Autofaturação",
                "TesteCheckBox" => "Envio de Teste",
                _ => "Ajuda"
            };
        }

        private void Window_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Keyboard.FocusedElement is DependencyObject focusedElement)
            {
                bool isInsideForm = false;
                DependencyObject? parent = focusedElement;

                while (parent != null)
                {
                    if (parent is TextBox || parent is PasswordBox || parent is ComboBox || parent is CheckBox)
                    {
                        isInsideForm = true;
                        break;
                    }

                    // Usa o VisualTree se possível, senão cai no LogicalTree
                    parent = parent is Visual || parent is Visual3D
                        ? VisualTreeHelper.GetParent(parent)
                        : LogicalTreeHelper.GetParent(parent);
                }

                if (!isInsideForm && string.IsNullOrWhiteSpace(FicheiroTextBox.Text))
                {
                    MostrarAjudaInicial();
                }
            }
        }

        private async void ReexecutarPreValidacao_Click(object sender, RoutedEventArgs e)
        {
            await ExecutarPreValidacaoAsync();
        }

        private bool ValidarCamposObrigatorios()
        {
            bool valido = true;

            void MarcarInvalido(Control ctrl, string mensagem)
            {
                ctrl.BorderBrush = Brushes.Red;
                ctrl.ToolTip = mensagem;
                valido = false;
            }

            void Limpar(Control ctrl)
            {
                ctrl.ClearValue(Border.BorderBrushProperty);
                ctrl.ToolTip = null;
            }

            Limpar(AnoTextBox);
            Limpar(MesTextBox);
            Limpar(NifTextBox);
            Limpar(PasswordBox);
            Limpar(FicheiroTextBox);

            if (string.IsNullOrWhiteSpace(AnoTextBox.Text))
                MarcarInvalido(AnoTextBox, "O ano é obrigatório.");

            if (string.IsNullOrWhiteSpace(MesTextBox.Text))
                MarcarInvalido(MesTextBox, "O mês é obrigatório.");

            if (string.IsNullOrWhiteSpace(NifTextBox.Text))
                MarcarInvalido(NifTextBox, "O NIF é obrigatório.");

            if (string.IsNullOrWhiteSpace(PasswordBox.Password))
                MarcarInvalido(PasswordBox, "A password é obrigatória.");

            if (string.IsNullOrWhiteSpace(FicheiroTextBox.Text))
                MarcarInvalido(FicheiroTextBox, "O ficheiro SAF-T é obrigatório.");

            return valido;
        }

        private void Campo_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe)
                AtualizarAjuda(GetCampoAjuda(fe.Name));
        }

        private void Campo_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is FrameworkElement fe)
                AtualizarAjuda(GetCampoAjuda(fe.Name));
        }

        private void RightTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = (RightTabControl.SelectedItem as TabItem)?.Header?.ToString();

            if (selected == "Histórico")
            {
                CarregarHistoricoEnvios(); // ou outro método que usares
            }
            else if (selected == "Resultado")
            {
                OutputTextBlock.ScrollToEnd(); // ou outro comportamento
            }
        }

        private void AbrirSite_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.loadinghappiness.pt",
                UseShellExecute = true
            });
        }

        private void CampoCorrigido(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.ClearValue(Border.BorderBrushProperty);
                tb.ToolTip = null;
            }
            else if (sender is PasswordBox pb)
            {
                pb.ClearValue(Border.BorderBrushProperty);
                pb.ToolTip = null;
            }
        }

        private async void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Ficheiros SAF-T (*.xml, *.zip, *.gz, *.tar, *.rar)|*.xml;*.zip;*.gz;*.tar;*.rar"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    LimparPastaTemporaria();
                    var password = ArquivoPasswordBox.Password;
                    var progress = new Progress<FileExtractionProgress>(p =>
                    {
                        if (!string.IsNullOrWhiteSpace(p.Mensagem))
                        {
                            _ticker.ShowMessage(p.Mensagem, TickerMessageType.Info);
                        }
                    });

                    var (caminhoXml, pastaTemp) = await Task.Run(() =>
                        FileExtractionHelper.ObterXmlDoFicheiro(dialog.FileName,
                            string.IsNullOrWhiteSpace(password) ? null : password,
                            progress));
                    FicheiroTextBox.Text = caminhoXml;
                    _pastaTemporaria = pastaTemp;
                    PreencherCamposDoFicheiroSafT(caminhoXml);
                    MostrarAnimacaoCabecalhoAjuda();
                    _ticker.ShowMessage("Ficheiro SAF-T carregado com sucesso.", TickerMessageType.Success);
                }
                catch (Exception ex)
                {
                    _ticker.ShowMessage($"Erro ao extrair ficheiro: {ex.Message}", TickerMessageType.Error);
                }
            }

            SelecionarTab("Ajuda");
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
                // Ignorar falhas de limpeza. A pasta será temporária e será substituída.
            }
            finally
            {
                _pastaTemporaria = null;
            }
        }

        private void BrowseOutputFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Ficheiro XML (*.xml)|*.xml",
                DefaultExt = ".xml"
            };

            if (dialog.ShowDialog() == true)
            {
                OutputTextBox.Text = dialog.FileName;
                _ticker.ShowMessage("Destino do ficheiro definido com sucesso.", TickerMessageType.Success);
            }
        }

        private void SelecionarTab(string header)
        {
            foreach (var item in RightTabControl.Items.OfType<TabItem>())
            {
                if (string.Equals(item.Header?.ToString(), header, StringComparison.OrdinalIgnoreCase))
                {
                    item.IsSelected = true;
                    break;
                }
            }
        }

        private async void Enviar_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidarCamposObrigatorios())
                return;

            string nif = NifTextBox.Text;
            string password = PasswordBox.Password;
            string ano = AnoTextBox.Text;
            string mes = MesTextBox.Text;
            string op = ((ComboBoxItem)OperacaoComboBox.SelectedItem)?.Content?.ToString() ?? "";
            string ficheiro = FicheiroTextBox.Text;
            string memoria = MemoriaTextBox.Text;
            string outputPath = OutputTextBox.Text;
            string nifEmitente = NifEmitenteTextBox.Text;
            bool isTeste = TesteCheckBox.IsChecked == true;
            bool isAf = AutoFaturacaoCheckBox.IsChecked == true;
            var etiquetas = EtiquetasTextBox.Text?
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            if (!File.Exists(ficheiro))
            {
                _ticker.ShowMessage("Ficheiro SAF-T não encontrado.", TickerMessageType.Error);
                return;
            }

            var jarUpdateResult = await JarUpdateService.EnsureLatestAsync(CancellationToken.None);

            string jarFileName = Path.GetFileName(jarUpdateResult.JarPath);

            if (!jarUpdateResult.Success)
            {
                var message = jarUpdateResult.Message ?? $"Não foi possível preparar o {jarFileName}.";
                _ticker.ShowMessage(message, jarUpdateResult.UsedFallback ? TickerMessageType.Warning : TickerMessageType.Error);

                if (!jarUpdateResult.UsedFallback)
                {
                    return;
                }
            }
            else
            {
                if (jarUpdateResult.Updated)
                {
                    _ticker.ShowMessage(jarUpdateResult.Message ?? $"Foi descarregada a versão mais recente do {jarFileName}.", TickerMessageType.Info);
                }
                else if (jarUpdateResult.UsedFallback)
                {
                    var message = jarUpdateResult.Message ?? $"Não foi possível confirmar atualizações do {jarFileName}; a versão local será utilizada.";
                    _ticker.ShowMessage(message, TickerMessageType.Warning);
                }
            }

            string jarPath = jarUpdateResult.JarPath;
            string updatePath = Path.GetDirectoryName(jarPath) ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libs");

            if (!File.Exists(jarPath))
            {
                _ticker.ShowMessage($"O ficheiro {jarFileName} não está disponível após a tentativa de atualização.", TickerMessageType.Error);
                return;
            }

            OutputTextBlock.Text = ""; // Limpa output anterior
            OutputSummaryTextBlock.Text = string.Empty;
            SelecionarTab("Resultado");

            try
            {
                var dataEnvio = DateTime.Now;
                var resultado = await Task.Run(() =>
                {
                    var psi = CriarProcessoEnvio(jarPath, nif, password, ano, mes, op, ficheiro, updatePath, memoria, isTeste, isAf, nifEmitente, outputPath);

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
                _ultimoResumo = resumo;
                string resumoLegivel = resumo.ConstruirResumoLegivel();
                OutputSummaryTextBlock.Text = string.IsNullOrWhiteSpace(resumoLegivel)
                    ? "Sem informação interpretável."
                    : resumoLegivel;

                var blocos = new List<string>();
                if (!string.IsNullOrWhiteSpace(output)) blocos.Add(output.Trim());
                if (!string.IsNullOrWhiteSpace(error)) blocos.Add(error.Trim());
                OutputTextBlock.Text = string.Join(Environment.NewLine + Environment.NewLine, blocos);

                bool sucesso = resumo.Sucesso;
                string resultadoFinal = isTeste ? "teste" : sucesso ? "sucesso" : "erro";

                _ticker.ShowMessage(
                    sucesso ? "Envio realizado com sucesso." : "Erro ao enviar ficheiro.",
                    sucesso ? TickerMessageType.Success : TickerMessageType.Error
                );

                // Guardar no histórico
                var entrada = new EnvioHistoricoEntry
                {
                    EmpresaNome = _nomeEmpresa,
                    NIF = nif,
                    DataHora = dataEnvio,
                    Ano = int.TryParse(ano, out var anoInt) ? anoInt : DateTime.Now.Year,
                    Mes = mes,
                    FicheiroSaft = ficheiro,
                    FicheiroOutput = outputPath,
                    Operacao = op,
                    Resultado = resultadoFinal,
                    Resumo = resumoLegivel,
                    Tags = etiquetas
                };

                entrada.LogFilePath = HistoricoEnviosService.GuardarLog(entrada, resumoLegivel, output, error);
                HistoricoEnviosService.RegistarEnvio(entrada);

                if (_historicoInicializado)
                {
                    _historicoCompleto.Add(entrada);
                    AplicarFiltrosHistorico();
                }
            }
            catch (Exception ex)
            {
                _ticker.ShowMessage($"Erro ao executar o envio: {ex.Message}", TickerMessageType.Error);
            }
            finally
            {
                LimparPastaTemporaria();
            }
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
                    NifTextBox.Text = nifNode.InnerText;

                var nameNode = doc.SelectSingleNode("//pt:Header/pt:BusinessName", nsMgr);
                if (nameNode != null)
                    _nomeEmpresa = nameNode.InnerText.Trim();
                else
                    _nomeEmpresa = "Desconhecida";

                var dataNode = doc.SelectSingleNode("//pt:SourceDocuments//pt:Invoice/pt:InvoiceDate", nsMgr) ??
                               doc.SelectSingleNode("//pt:SourceDocuments//pt:Transaction/pt:TransactionDate", nsMgr);

                if (dataNode != null && DateTime.TryParse(dataNode.InnerText, out var data))
                {
                    AnoTextBox.Text = data.Year.ToString();
                    MesTextBox.Text = data.Month.ToString("D2");
                }
            }
            catch (Exception ex)
            {
                _ticker.ShowMessage($"Erro ao ler ficheiro SAF-T: {ex.Message}", TickerMessageType.Warning);
            }
        }

        private void CarregarHistoricoEnvios()
        {
            if (!_historicoInicializado)
            {
                _historicoCompleto.Clear();
                var lista = HistoricoEnviosService.ObterHistorico();
                _historicoCompleto.AddRange(lista);
                AtualizarOpcoesFiltrosHistorico();
                _historicoInicializado = true;
            }

            AplicarFiltrosHistorico();
        }

        private void AtualizarOpcoesFiltrosHistorico()
        {
            var empresas = _historicoCompleto
                .Select(e => e.EmpresaNome)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(e => e)
                .ToList();

            var operacoes = _historicoCompleto
                .Select(e => e.Operacao)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(e => e)
                .ToList();

            var resultados = _historicoCompleto
                .Select(e => e.Resultado)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(e => e)
                .ToList();

            EmpresaComboBox.ItemsSource = empresas;
            OperacaoHistoricoComboBox.ItemsSource = operacoes;
            ResultadoHistoricoComboBox.ItemsSource = resultados;

            EmpresaComboBox.SelectedIndex = -1;
            OperacaoHistoricoComboBox.SelectedIndex = -1;
            ResultadoHistoricoComboBox.SelectedIndex = -1;
        }

        private void AplicarFiltrosHistorico()
        {
            IEnumerable<EnvioHistoricoEntry> query = _historicoCompleto;

            string empresaFiltro = EmpresaComboBox.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(empresaFiltro))
            {
                query = query.Where(e => string.Equals(e.EmpresaNome, empresaFiltro, StringComparison.OrdinalIgnoreCase));
            }

            string operacaoFiltro = OperacaoHistoricoComboBox.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(operacaoFiltro))
            {
                query = query.Where(e => string.Equals(e.Operacao, operacaoFiltro, StringComparison.OrdinalIgnoreCase));
            }

            string resultadoFiltro = ResultadoHistoricoComboBox.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(resultadoFiltro))
            {
                query = query.Where(e => string.Equals(e.Resultado, resultadoFiltro, StringComparison.OrdinalIgnoreCase));
            }

            if (HistoricoInicioDatePicker.SelectedDate is DateTime inicio)
            {
                query = query.Where(e => e.DataHora >= inicio.Date);
            }

            if (HistoricoFimDatePicker.SelectedDate is DateTime fim)
            {
                var fimInclusivo = fim.Date.AddDays(1).AddTicks(-1);
                query = query.Where(e => e.DataHora <= fimInclusivo);
            }

            var etiquetasFiltro = EtiquetaFiltroTextBox.Text?
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            if (etiquetasFiltro.Any())
            {
                query = query.Where(e => e.Tags != null && etiquetasFiltro.All(tag => e.Tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase))));
            }

            _historicoFiltrado = query
                .OrderByDescending(e => e.DataHora)
                .ToList();

            HistoricoDataGrid.ItemsSource = _historicoFiltrado;
            HistoricoEstatisticasTextBlock.Text = GerarEstatisticasHistorico(_historicoFiltrado);
        }

        private string GerarEstatisticasHistorico(IEnumerable<EnvioHistoricoEntry> entradas)
        {
            var lista = entradas.ToList();
            if (!lista.Any())
            {
                return "Sem registos para apresentar.";
            }

            int total = lista.Count;
            int sucesso = lista.Count(e => string.Equals(e.Resultado, "sucesso", StringComparison.OrdinalIgnoreCase));
            int erro = lista.Count(e => string.Equals(e.Resultado, "erro", StringComparison.OrdinalIgnoreCase));
            int teste = lista.Count(e => string.Equals(e.Resultado, "teste", StringComparison.OrdinalIgnoreCase));

            var sb = new StringBuilder();
            sb.AppendLine($"Total: {total} | Sucesso: {sucesso} | Erros: {erro} | Testes: {teste}");
            sb.AppendLine($"Taxa de sucesso global: {(total > 0 ? sucesso * 100.0 / total : 0):F1}%");

            var melhores = lista
                .GroupBy(e => e.EmpresaNome)
                .OrderByDescending(g => g.Count())
                .Take(5);

            sb.AppendLine("Taxa de sucesso por empresa:");
            foreach (var grupo in melhores)
            {
                int totalEmpresa = grupo.Count();
                int sucessoEmpresa = grupo.Count(e => string.Equals(e.Resultado, "sucesso", StringComparison.OrdinalIgnoreCase));
                double taxa = totalEmpresa > 0 ? sucessoEmpresa * 100.0 / totalEmpresa : 0;
                sb.AppendLine($" • {grupo.Key}: {taxa:F1}% ({sucessoEmpresa}/{totalEmpresa})");
            }

            return sb.ToString().Trim();
        }

        private void FiltroHistorico_Alterado(object sender, SelectionChangedEventArgs e)
        {
            if (!_historicoInicializado)
                return;

            AplicarFiltrosHistorico();
        }

        private void FiltroHistorico_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_historicoInicializado)
                return;

            AplicarFiltrosHistorico();
        }

        private void LimparFiltrosHistorico_Click(object sender, RoutedEventArgs e)
        {
            EmpresaComboBox.Text = string.Empty;
            OperacaoHistoricoComboBox.SelectedIndex = -1;
            OperacaoHistoricoComboBox.Text = string.Empty;
            ResultadoHistoricoComboBox.SelectedIndex = -1;
            ResultadoHistoricoComboBox.Text = string.Empty;
            HistoricoInicioDatePicker.SelectedDate = null;
            HistoricoFimDatePicker.SelectedDate = null;
            EtiquetaFiltroTextBox.Text = string.Empty;

            AplicarFiltrosHistorico();
        }

        private void ExportarHistorico_Click(object sender, RoutedEventArgs e)
        {
            if (_historicoFiltrado == null || !_historicoFiltrado.Any())
            {
                _ticker.ShowMessage("Não existem registos para exportar.", TickerMessageType.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Ficheiro CSV (*.csv)|*.csv",
                FileName = $"historico_envios_{DateTime.Now:yyyyMMddHHmm}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                HistoricoEnviosService.ExportarCsv(_historicoFiltrado, dialog.FileName);
                _ticker.ShowMessage("Histórico exportado com sucesso.", TickerMessageType.Success);
            }
        }

        private void MostrarAnimacaoCabecalhoAjuda()
        {
            HelpHeaderBlock.Opacity = 0;
            var transform = new TranslateTransform { Y = 10 };
            HelpHeaderBlock.RenderTransform = transform;

            var opacityAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.4),
                BeginTime = TimeSpan.FromSeconds(1.5),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            var moveAnimation = new DoubleAnimation
            {
                From = 10,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.4),
                BeginTime = TimeSpan.FromSeconds(1.5),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            HelpHeaderBlock.BeginAnimation(OpacityProperty, opacityAnimation);
            transform.BeginAnimation(TranslateTransform.YProperty, moveAnimation);
        }

        private void MostrarLogoComAnimacao()
        {
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(1),
                BeginTime = TimeSpan.FromSeconds(3),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            LogoContainer.BeginAnimation(OpacityProperty, fadeIn);
        }
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
    }
}