namespace ChamadosManutencao.Domain.Common;

/// <summary>
/// Violacao de invariante ou de regra de negocio detectada dentro do dominio.
/// A API traduz esta excecao para ProblemDetails com status 422.
/// </summary>
public class ExcecaoDeDominio : Exception
{
    public ExcecaoDeDominio(string mensagem) : base(mensagem)
    {
    }

    public ExcecaoDeDominio(string mensagem, string? requisito) : base(mensagem) =>
        Requisito = requisito;

    /// <summary>Identificador do requisito violado (ex.: "RN0034"), quando aplicavel.</summary>
    public string? Requisito { get; }
}
