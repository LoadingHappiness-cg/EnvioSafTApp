using EnvioSafTApp.Models;
using EnvioSafTApp.Services;
using EnvioSafTApp.Services.Interfaces;
using EnvioSafTApp.ViewModels;
using Xunit;

namespace EnvioSafTApp.ViewModels.Tests;

public sealed class MainViewModelEnviarCommandTests
{
    [Fact]
    public async Task EnviarCommand_WhenJarPreparationThrows_DoesNotPropagateException()
    {
        var tempDir = CreateTempDir();
        try
        {
            var xmlPath = Path.Combine(tempDir, "saft.xml");
            await File.WriteAllTextAsync(xmlPath, "<AuditFile />");

            var vm = CreateViewModel(new ThrowingJarUpdateService());
            vm.Ano = "2026";
            vm.Mes = "01";
            vm.Nif = "123456789";
            vm.Password = "secret";
            vm.FicheiroSafT = xmlPath;

            await vm.EnviarCommand.ExecuteAsync(null);

            Assert.False(vm.IsSending);
            Assert.Equal(TickerMessageType.Error, vm.TickerType);
            Assert.Contains("Erro ao executar o envio", vm.TickerMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static MainViewModel CreateViewModel(IJarUpdateService jarUpdateService)
        => new(
            jarUpdateService,
            new StubPreflightCheckService(),
            new StubHistoricoEnviosService(),
            new StubSaftValidationService(),
            new StubClipboardService(),
            new StubFileService());

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "main-vm-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class ThrowingJarUpdateService : IJarUpdateService
    {
        public string GetLocalJarPath() => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "EnviaSaft.jar");

        public Task<JarUpdateResult> EnsureLatestAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("network down");

        public Task<(string? SavedPath, bool IsNew)> RememberJarAsync(string? sourceJarPath, CancellationToken cancellationToken)
            => Task.FromResult<(string? SavedPath, bool IsNew)>((null, false));
    }

    private sealed class StubPreflightCheckService : IPreflightCheckService
    {
        public Task<IReadOnlyList<PreflightCheckResult>> RunAsync()
            => Task.FromResult<IReadOnlyList<PreflightCheckResult>>(Array.Empty<PreflightCheckResult>());
    }

    private sealed class StubSaftValidationService : ISaftValidationService
    {
        public Task<SaftValidationResult> ValidateAsync(string xmlPath, CancellationToken cancellationToken)
            => Task.FromResult(new SaftValidationResult
            {
                Sucesso = true,
                Resumo = "ok",
                EsquemaDisponivel = true,
                OrigemXsd = "stub",
                Problemas = new List<SaftValidationIssue>(),
                Sugestoes = new List<string>()
            });
    }

    private sealed class StubClipboardService : IClipboardService
    {
        public Task<bool> SetTextAsync(string text, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class StubFileService : IFileService
    {
        public Task<string?> OpenFileDialogAsync(string filter) => Task.FromResult<string?>(null);
        public Task<string?> SaveFileDialogAsync(string filter, string defaultExt) => Task.FromResult<string?>(null);
    }

    private sealed class StubHistoricoEnviosService : IHistoricoEnviosService
    {
        private readonly string _baseFolder = Path.Combine(Path.GetTempPath(), "historico-tests-" + Guid.NewGuid().ToString("N"));

        public string BaseFolder => _baseFolder;

        public void RegistarEnvio(EnvioHistoricoEntry entry)
        {
        }

        public List<EnvioHistoricoEntry> ObterHistorico() => new();

        public string GuardarLog(EnvioHistoricoEntry entry, string resumo, string output, string error)
            => Path.Combine(_baseFolder, "dummy.log");

        public void ExportarCsv(IEnumerable<EnvioHistoricoEntry> entradas, string destino)
        {
        }
    }
}
