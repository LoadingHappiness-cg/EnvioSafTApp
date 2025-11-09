using System.Collections.Generic;

namespace EnvioSafTApp.Models
{
    public class HelpEntry
    {
        public string? Campo { get; set; }
        public string? Descricao { get; set; }

        public static List<HelpEntry> GetDefaultEntries()
        {
            return new List<HelpEntry>
            {
                new() { Campo = "Ano / Mês", Descricao = "Indique o ano e mês do período de envio." },
                new() { Campo = "NIF / Password", Descricao = "Introduza o NIF e a palavra-passe para autenticação no portal da AT." },
                new() { Campo = "Operação", Descricao = "Escolha a operação desejada: enviar ou validar o ficheiro SAF-T." },
                new() { Campo = "Ficheiro SAF-T", Descricao = "Selecione o ficheiro SAF-T que pretende enviar. Deve estar em formato XML." },
                new() { Campo = "Password do Arquivo", Descricao = "Se o ficheiro estiver comprimido com password, indique-a aqui para permitir a extração." },
                new() { Campo = "NIF Emitente", Descricao = "Se o ficheiro for emitido por outra entidade, indique o respetivo NIF." },
                new() { Campo = "Ficheiro de Retorno", Descricao = "Pode indicar um nome e localização para guardar o ficheiro de resposta." },
                new() { Campo = "Memória", Descricao = "Permite definir a memória máxima (ex: -Xmx512m) usada pela execução Java." },
                new() { Campo = "Etiquetas", Descricao = "Adicione etiquetas separadas por vírgulas para facilitar a filtragem no histórico." },
                new() { Campo = "Autofaturação", Descricao = "Ativa o parâmetro -af, para ficheiros gerados em regime de autofaturação." },
                new() { Campo = "Envio de Teste", Descricao = "Simula o envio sem o submeter à AT, útil para verificar erros." }
            };
        }
    }
}