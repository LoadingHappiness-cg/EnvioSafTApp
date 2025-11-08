using EnvioSafTApp.Models;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EnvioSafTApp.Services
{
    public static class FileExtractionHelper
    {
        private const int MaxNestedDepth = 4;

        private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".rar", ".tar", ".gz", ".tgz", ".tar.gz", ".7z"
        };

        public static (string caminhoXml, string? pastaTemp) ObterXmlDoFicheiro(
            string caminhoOriginal,
            string? arquivoPassword = null,
            IProgress<FileExtractionProgress>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(caminhoOriginal))
            {
                throw new ArgumentException("Caminho do ficheiro inválido.", nameof(caminhoOriginal));
            }

            var info = new FileInfo(caminhoOriginal);
            if (!info.Exists)
            {
                throw new FileNotFoundException("Ficheiro não encontrado.", caminhoOriginal);
            }

            if (info.Length == 0)
            {
                throw new InvalidOperationException("O ficheiro está vazio.");
            }

            if (string.Equals(info.Extension, ".xml", StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report(new FileExtractionProgress { Mensagem = "Ficheiro XML pronto para leitura.", Percentagem = 1 });
                return (caminhoOriginal, null);
            }

            string pastaTemp = Path.Combine(Path.GetTempPath(), "EnviaSaft_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(pastaTemp);

            progress?.Report(new FileExtractionProgress { Mensagem = "A extrair arquivo...", Percentagem = 0 });
            ExtrairArquivoRecursivo(caminhoOriginal, pastaTemp, arquivoPassword, progress, 0);

            string? ficheiroXml = Directory.GetFiles(pastaTemp, "*.xml", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (ficheiroXml == null)
            {
                throw new InvalidOperationException("Nenhum ficheiro .xml foi encontrado dentro do arquivo.");
            }

            progress?.Report(new FileExtractionProgress { Mensagem = "Ficheiro SAF-T localizado.", Percentagem = 1 });
            return (ficheiroXml, pastaTemp);
        }

        private static void ExtrairArquivoRecursivo(
            string caminhoArquivo,
            string pastaDestino,
            string? password,
            IProgress<FileExtractionProgress>? progress,
            int profundidade)
        {
            if (profundidade > MaxNestedDepth)
            {
                throw new InvalidOperationException("Arquivo contém níveis de compactação demasiados. Extrai manualmente e tente novamente.");
            }

            using var stream = File.OpenRead(caminhoArquivo);
            var readerOptions = new ReaderOptions
            {
                Password = password,
                LookForHeader = true
            };

            using var reader = ReaderFactory.Open(stream, readerOptions);
            while (reader.MoveToNextEntry())
            {
                if (reader.Entry.IsDirectory)
                {
                    continue;
                }

                if (reader.Entry.IsEncrypted && string.IsNullOrEmpty(password))
                {
                    throw new InvalidOperationException("O arquivo está protegido por palavra-passe. Indique a password na aplicação para proceder à extração.");
                }

                string destino = Path.Combine(pastaDestino, reader.Entry.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(destino) ?? pastaDestino);

                reader.WriteEntryToFile(destino, new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = true
                });

                progress?.Report(new FileExtractionProgress
                {
                    Mensagem = $"Extraído: {reader.Entry.Key}",
                });

                string extensaoExtraido = Path.GetExtension(destino);
                if (ArchiveExtensions.Contains(extensaoExtraido))
                {
                    progress?.Report(new FileExtractionProgress
                    {
                        Mensagem = $"A analisar arquivo aninhado: {reader.Entry.Key}",
                    });

                    ExtrairArquivoRecursivo(destino, pastaDestino, password, progress, profundidade + 1);
                }
            }
        }
    }
}
