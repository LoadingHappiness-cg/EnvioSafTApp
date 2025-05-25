using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.IO;
using System.Linq;

namespace EnvioSafTApp.Services
{
    public static class FileExtractionHelper
    {
        public static (string caminhoXml, string pastaTemp) ObterXmlDoFicheiro(string caminhoOriginal)
        {
            string extensao = Path.GetExtension(caminhoOriginal).ToLowerInvariant();
            string pastaTemp = Path.Combine(Path.GetTempPath(), "EnviaSaft_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(pastaTemp);

            if (extensao == ".xml")
            {
                return (caminhoOriginal, null);
            }

            using var stream = File.OpenRead(caminhoOriginal);
            IReader leitor;

            if (extensao == ".gz")
            {
                leitor = ReaderFactory.Open(stream);
                while (leitor.MoveToNextEntry())
                {
                    if (!leitor.Entry.IsDirectory && leitor.Entry.Key.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        string destino = Path.Combine(pastaTemp, Path.GetFileName(leitor.Entry.Key));
                        leitor.WriteEntryToFile(destino);
                        return (destino, pastaTemp);
                    }
                }
            }
            else if (extensao == ".zip" || extensao == ".rar" || extensao == ".tar")
            {
                using var arquivo = ArchiveFactory.Open(stream);
                var entrada = arquivo.Entries.FirstOrDefault(e =>
                    !e.IsDirectory && e.Key.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

                if (entrada != null)
                {
                    string destino = Path.Combine(pastaTemp, Path.GetFileName(entrada.Key));
                    entrada.WriteToFile(destino);
                    return (destino, pastaTemp);
                }
            }

            throw new InvalidOperationException("Nenhum ficheiro .xml foi encontrado dentro do arquivo.");
        }
    }
}