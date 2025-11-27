namespace EnvioSafTApp.Services.Interfaces
{
    public interface IFileService
    {
        string? OpenFileDialog(string filter);
        string? SaveFileDialog(string filter, string defaultExt);
    }
}
