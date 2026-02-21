namespace EnvioSafTApp.Services.Interfaces
{
    public interface IFileService
    {
        Task<string?> OpenFileDialogAsync(string filter);
        Task<string?> SaveFileDialogAsync(string filter, string defaultExt);
    }
}
