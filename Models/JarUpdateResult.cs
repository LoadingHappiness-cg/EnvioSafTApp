using System;

namespace EnvioSafTApp.Models
{
    public class JarUpdateResult
    {
        public bool Success { get; set; }
        public bool Updated { get; set; }
        public bool UsedFallback { get; set; }
        public string? Message { get; set; }
        public string? ErrorMessage { get; set; }
        public string JarPath { get; set; } = string.Empty;
        public DateTimeOffset? RemoteLastModified { get; set; }
    }
}
