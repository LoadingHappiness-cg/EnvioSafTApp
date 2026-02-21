using System.Net;
using System.Net.Http;
using System.Text;
using EnvioSafTApp.Services;
using Xunit;

namespace EnvioSafTApp.Tests;

public sealed class SaftValidationServiceTests
{
    [Fact]
    public async Task ValidateAsync_UsesDownloadedSchema_WhenLocalSchemaIsInvalid()
    {
        var tempDir = CreateTempDir();
        try
        {
            var xmlPath = Path.Combine(tempDir, "saft.xml");
            await File.WriteAllTextAsync(xmlPath, "<AuditFile>ok</AuditFile>", Encoding.UTF8);

            var schemaPath = Path.Combine(tempDir, "SAFTPT1.04_01.xsd");
            await File.WriteAllTextAsync(schemaPath, "not a schema", Encoding.UTF8);

            var downloadedSchema = """
                <?xml version="1.0" encoding="utf-8"?>
                <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" elementFormDefault="qualified">
                  <xs:element name="AuditFile">
                    <xs:complexType>
                      <xs:simpleContent>
                        <xs:extension base="xs:string">
                          <xs:assert test=". != ''" />
                        </xs:extension>
                      </xs:simpleContent>
                    </xs:complexType>
                  </xs:element>
                </xs:schema>
                """;

            using var httpClient = new HttpClient(new StubHttpHandler(downloadedSchema));
            var service = new SaftValidationService(schemaPath, new[] { "https://example.test/saft.xsd" }, httpClient);

            var result = await service.ValidateAsync(xmlPath, CancellationToken.None);

            Assert.True(result.EsquemaDisponivel);
            Assert.True(result.Sucesso);
            Assert.Empty(result.Problemas);

            var persistedSchema = await File.ReadAllTextAsync(schemaPath, Encoding.UTF8);
            Assert.DoesNotContain("assert", persistedSchema, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ValidateAsync_UsesLocalSchemaBySanitizingAssertElements_WithoutNetwork()
    {
        var tempDir = CreateTempDir();
        try
        {
            var xmlPath = Path.Combine(tempDir, "saft.xml");
            await File.WriteAllTextAsync(xmlPath, "<AuditFile>ok</AuditFile>", Encoding.UTF8);

            var schemaPath = Path.Combine(tempDir, "SAFTPT1.04_01.xsd");
            var localSchemaWithAssert = """
                <?xml version="1.0" encoding="utf-8"?>
                <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" elementFormDefault="qualified">
                  <xs:element name="AuditFile">
                    <xs:complexType>
                      <xs:simpleContent>
                        <xs:extension base="xs:string">
                          <xs:assert test=". != ''" />
                        </xs:extension>
                      </xs:simpleContent>
                    </xs:complexType>
                  </xs:element>
                </xs:schema>
                """;
            await File.WriteAllTextAsync(schemaPath, localSchemaWithAssert, Encoding.UTF8);

            using var httpClient = new HttpClient(new FailingHttpHandler());
            var service = new SaftValidationService(schemaPath, new[] { "https://example.test/never-called.xsd" }, httpClient);

            var result = await service.ValidateAsync(xmlPath, CancellationToken.None);

            Assert.True(result.EsquemaDisponivel);
            Assert.True(result.Sucesso);
            Assert.Empty(result.Problemas);

            var persistedSchema = await File.ReadAllTextAsync(schemaPath, Encoding.UTF8);
            Assert.DoesNotContain("assert", persistedSchema, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ValidateAsync_ReturnsWarningMode_WhenSchemaIsUnavailableButXmlIsWellFormed()
    {
        var tempDir = CreateTempDir();
        try
        {
            var xmlPath = Path.Combine(tempDir, "saft.xml");
            await File.WriteAllTextAsync(xmlPath, "<AuditFile><Node>ok</Node></AuditFile>", Encoding.UTF8);

            var schemaPath = Path.Combine(tempDir, "SAFTPT1.04_01.xsd");
            using var httpClient = new HttpClient(new FailingHttpHandler());
            var service = new SaftValidationService(schemaPath, new[] { "https://example.test/unavailable.xsd" }, httpClient);

            var result = await service.ValidateAsync(xmlPath, CancellationToken.None);

            Assert.False(result.EsquemaDisponivel);
            Assert.True(result.Sucesso);
            Assert.Contains("XML válido", result.Resumo, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(result.Problemas);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "saft-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class StubHttpHandler(string responseContent) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes(responseContent))
            };
            return Task.FromResult(response);
        }
    }

    private sealed class FailingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("network unavailable");
    }
}
