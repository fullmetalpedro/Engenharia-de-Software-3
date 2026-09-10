using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC04Chamados;

namespace ChamadosManutencao.Api.Endpoints;

/// <summary>
/// Leitura das requisicoes que trazem arquivos junto dos campos (decisao D07: a abertura do
/// chamado precisa receber as fotos na mesma requisicao para atender a RN0032).
///
/// A requisicao pode chegar como multipart/form-data (com arquivos) ou como JSON (sem
/// arquivos); os dois formatos sao aceitos.
/// </summary>
public static class LeitorDeFormulario
{
    public static bool EhFormulario(HttpRequest requisicao) => requisicao.HasFormContentType;

    public static async Task<(AbrirChamadoCommand Comando, List<ArquivoRecebido> Anexos)>
        LerAberturaDeChamadoAsync(HttpRequest requisicao, CancellationToken cancellationToken)
    {
        if (!EhFormulario(requisicao))
        {
            var doJson = await requisicao.ReadFromJsonAsync<AbrirChamadoCommand>(cancellationToken)
                ?? throw new ValidacaoException("corpo", "Corpo da requisicao ausente ou invalido.");

            return (doJson, []);
        }

        var formulario = await requisicao.ReadFormAsync(cancellationToken);

        var comando = new AbrirChamadoCommand(
            LerGuid(formulario, "imovelId"),
            LerGuid(formulario, "categoriaServicoId"),
            LerGuid(formulario, "tipoServicoId"),
            formulario["descricaoProblema"].ToString(),
            LerBool(formulario, "indicacaoDeRisco"));

        return (comando, LerArquivos(formulario));
    }

    public static List<ArquivoRecebido> LerArquivos(IFormCollection formulario) =>
        formulario.Files
            .Select(arquivo => new ArquivoRecebido(
                arquivo.FileName,
                arquivo.ContentType,
                arquivo.Length,
                arquivo.OpenReadStream()))
            .ToList();

    private static Guid LerGuid(IFormCollection formulario, string campo)
    {
        var valor = formulario[campo].ToString();

        return Guid.TryParse(valor, out var id)
            ? id
            : throw new ValidacaoException(campo, $"O campo {campo} e obrigatorio e deve ser um GUID.");
    }

    private static bool LerBool(IFormCollection formulario, string campo) =>
        bool.TryParse(formulario[campo].ToString(), out var valor) && valor;
}
