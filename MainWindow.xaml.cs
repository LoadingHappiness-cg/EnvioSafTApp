using EnvioSafTApp.Models;
using EnvioSafTApp.Services;
using Microsoft.Win32;
using SharpCompress.Archives;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        private StatusTickerService _ticker;
        private string _nomeEmpresa = "Desconhecida";
        private string _pastaTemporaria;
        public MainWindow()
        {
            InitializeComponent();
            AppVersionTextBlock.Text = $"v{ObterVersao()} – loadinghappiness.pt";
            _ticker = new StatusTickerService(StatusTicker, StatusTickerIcon, StatusTickerBorder);
            MostrarAjudaInicial();
            MostrarAnimacaoCabecalhoAjuda();
            MostrarLogoComAnimacao();
            this.PreviewMouseDown += Window_PreviewMouseDown;
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

        private string GetCampoAjuda(string nomeCampo)
        {
            return nomeCampo switch
            {
                "AnoTextBox" or "MesTextBox" => "Ano / Mês",
                "NifTextBox" or "PasswordBox" => "NIF / Password",
                "OperacaoComboBox" => "Operação",
                "FicheiroTextBox" => "Ficheiro SAF-T",
                "NifEmitenteTextBox" => "NIF Emitente",
                "OutputTextBox" => "Ficheiro de Retorno",
                "MemoriaTextBox" => "Memória",
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

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Ficheiros SAF-T (*.xml, *.zip, *.gz, *.tar, *.rar)|*.xml;*.zip;*.gz;*.tar;*.rar"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var (caminhoXml, pastaTemp) = FileExtractionHelper.ObterXmlDoFicheiro(dialog.FileName);
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

            RightTabControl.SelectedIndex = 0;
        }

        public static (string caminhoXml, string pastaTemp) ObterXmlDoFicheiro(string caminhoOriginal)
        {
            var extensao = Path.GetExtension(caminhoOriginal).ToLowerInvariant();

            if (extensao == ".xml")
                return (caminhoOriginal, null);

            string pastaTemp = Path.Combine(Path.GetTempPath(), "EnviaSaft", Guid.NewGuid().ToString());
            Directory.CreateDirectory(pastaTemp);

            if (extensao == ".gz")
            {
                using var stream = File.OpenRead(caminhoOriginal);
                using var reader = ReaderFactory.Open(stream);
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        string destino = Path.Combine(pastaTemp, reader.Entry.Key);
                        reader.WriteEntryToFile(destino);
                    }
                }
            }
            else if (extensao is ".zip" or ".rar" or ".tar" or ".tgz" or ".tar.gz")
            {
                using var archive = ArchiveFactory.Open(caminhoOriginal);
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    string destino = Path.Combine(pastaTemp, entry.Key);
                    entry.WriteToFile(destino);
                }
            }

            var ficheiroXml = Directory.GetFiles(pastaTemp, "*.xml", SearchOption.AllDirectories).FirstOrDefault();
            if (ficheiroXml == null)
                throw new Exception("Nenhum ficheiro .xml encontrado após extração.");

            return (ficheiroXml, pastaTemp);
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

            if (!File.Exists(ficheiro))
            {
                _ticker.ShowMessage("Ficheiro SAF-T não encontrado.", TickerMessageType.Error);
                return;
            }

            string jarPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libs", "EnviaSaft.jar");
            string updatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libs");

            if (!File.Exists(jarPath))
            {
                _ticker.ShowMessage("O ficheiro EnviaSaft.jar não foi encontrado na pasta 'libs'.", TickerMessageType.Error);
                return;
            }

            string args = ConstruirArgumentosEnvio(jarPath, nif, password, ano, mes, op, ficheiro, updatePath, memoria, isTeste, isAf, nifEmitente, outputPath);

            OutputTextBlock.Text = ""; // Limpa output anterior
            RightTabControl.SelectedIndex = 1;

            try
            {
                var resultado = await Task.Run(() =>
                {
                    var psi = new ProcessStartInfo("java", args)
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

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

                OutputTextBlock.Text = output + Environment.NewLine + error;

                bool sucesso = !string.IsNullOrWhiteSpace(output);
                string resultadoFinal = isTeste ? "teste" : sucesso ? "sucesso" : "erro";

                _ticker.ShowMessage(
                    sucesso ? "Envio realizado com sucesso." : "Erro ao enviar ficheiro.",
                    sucesso ? TickerMessageType.Success : TickerMessageType.Error
                );

                // Guardar no histórico
                HistoricoEnviosService.RegistarEnvio(new EnvioHistoricoEntry
                {
                    EmpresaNome = _nomeEmpresa,
                    NIF = nif,
                    DataHora = DateTime.Now,
                    Ano = int.TryParse(ano, out var anoInt) ? anoInt : DateTime.Now.Year,
                    Mes = mes,
                    FicheiroSaft = ficheiro,
                    FicheiroOutput = outputPath,
                    Operacao = op,
                    Resultado = resultadoFinal
                });
            }
            catch (Exception ex)
            {
                _ticker.ShowMessage($"Erro ao executar o envio: {ex.Message}", TickerMessageType.Error);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(_pastaTemporaria) && Directory.Exists(_pastaTemporaria))
                {
                    try
                    {
                        Directory.Delete(_pastaTemporaria, recursive: true);
                    }
                    catch
                    {
                        // Ignorar erros de limpeza
                    }
                }
            }
        }

        private string ConstruirArgumentosEnvio(
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
            var args = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(memoria))
                args.Append($"{memoria} ");

            args.Append($"-jar \"{jarPath}\" -n {nif} -p {password} -a {ano} -m {mes} -op {op} -i \"{ficheiro}\" -c \"{updatePath}\"");

            if (isTeste)
                args.Append(" -t");

            if (isAf)
                args.Append(" -af");

            if (!string.IsNullOrWhiteSpace(nifEmitente))
                args.Append($" -ea {nifEmitente}");

            if (!string.IsNullOrWhiteSpace(outputPath))
                args.Append($" -o \"{outputPath}\"");

            return args.ToString();
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
            var lista = new List<EnvioHistoricoEntry>();
            string baseFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EnviaSaft",
                "HistoricoEnvios"
            );

            if (Directory.Exists(baseFolder))
            {
                foreach (var empresaDir in Directory.GetDirectories(baseFolder))
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
                                        entry.EmpresaNome = empresa; // opcional se já estiver no ficheiro
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
            }

            var empresasUnicas = lista
                .Select(e => e.EmpresaNome)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            EmpresaComboBox.ItemsSource = empresasUnicas;
            HistoricoDataGrid.ItemsSource = lista.OrderByDescending(e => e.DataHora).ToList();
        }

        private void FiltrarHistorico_Click(object sender, RoutedEventArgs e)
        {
            string empresaSelecionada = EmpresaComboBox.SelectedValue as string;

            if (!string.IsNullOrWhiteSpace(empresaSelecionada))
            {
                var listaFiltrada = HistoricoDataGrid.ItemsSource as List<EnvioHistoricoEntry>;

                if (listaFiltrada != null)
                {
                    var filtrados = listaFiltrada
                        .Where(e => e.EmpresaNome.Equals(empresaSelecionada, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(e => e.DataHora)
                        .ToList();

                    HistoricoDataGrid.ItemsSource = filtrados;
                }
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