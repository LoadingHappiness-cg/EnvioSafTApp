# EnviaSaft 📤

**EnviaSaft** é uma aplicação desktop gratuita desenvolvida em .NET/Avalonia que facilita o envio de ficheiros SAF-T para a Autoridade Tributária, utilizando a aplicação oficial *FactemiCLI* da AT.

---

## 🚀 Funcionalidades

- Suporte a ficheiros `.xml`, `.zip`, `.rar`, `.gz`, `.tar`
- Extração automática e envio do ficheiro SAF-T interno
- Validação do ficheiro SAF-T contra o XSD oficial da AT, com resumo e sugestões de correção
- Histórico de envios por empresa/ano/mês
- Animações e ajuda contextual
- Registo automático de envios e ficheiros de resposta
- Suporte a envio de teste (com ícone próprio no histórico)

---

## 📦 Requisitos

- Java instalado (para correr o cliente oficial `.jar` da AT — o nome do ficheiro pode variar)
- .NET 8.0 SDK (para desenvolvimento) ou build self-contained
- Windows 10/11 ou macOS (Apple Silicon)
- (Opcional) Acesso à internet no primeiro arranque para descarregar automaticamente o XSD oficial SAFTPT1.04_01.xsd. Se preferir, coloque manualmente esse ficheiro em `C:\Users\<UTILIZADOR>\AppData\Roaming\EnviaSaft\schemas`.

---

## ⚙️ Como usar

1. Seleciona um ficheiro SAF-T (ou um `.zip`, `.tar`, `.rar`, etc.)
2. Preenche os dados obrigatórios (NIF, password, ano, mês)
3. Envia e obtém o resultado em tempo real
4. Consulta os envios anteriores na aba **Histórico**
5. (Opcional) Antes de enviar, usa a aba **Pré-validação** para validar o ficheiro SAF-T contra o XSD oficial e seguir as sugestões de correção apresentadas

---

## 🛠️ Desenvolvimento

- Requer o .NET 8 SDK.
- Requer Java para executar o cliente oficial da AT.

Se o comando `dotnet` não estiver disponível no seu ambiente de desenvolvimento ou de CI pode usar o script `scripts/install-dotnet.sh`:

```bash
# Instala o SDK 8.0 (omissão) em ~/.dotnet
scripts/install-dotnet.sh

# (Opcional) instala outro canal, por exemplo 7.0
scripts/install-dotnet.sh 7.0

# Depois de executar o script adicione o SDK ao PATH
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
```

Se estiver atrás de um proxy HTTP/HTTPS configure `HTTP_PROXY` e `HTTPS_PROXY` antes de correr o script.

## 🍎 Bundle `.app` (macOS)

Para gerar uma aplicação nativa `.app` para Apple Silicon:

```bash
scripts/build-macos-app.sh
```

O bundle será criado em:

```bash
dist/macos/EnvioSafTApp.app
```

Para abrir no Finder/terminal:

```bash
open dist/macos/EnvioSafTApp.app
```

---

## 🆓 Licença

Distribuição gratuita. Desenvolvido por [Loading Happiness](https://loadinghappiness.com).

---
