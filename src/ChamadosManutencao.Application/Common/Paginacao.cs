namespace ChamadosManutencao.Application.Common;

/// <summary>
/// Parametros de paginacao obrigatorios em toda consulta de lista (RNF0011).
/// O tamanho maximo de pagina e 100.
/// </summary>
public sealed record ParametrosDePaginacao
{
    public const int TamanhoMaximoDePagina = 100;
    public const int TamanhoPadraoDePagina = 20;

    public ParametrosDePaginacao(int? page = null, int? pageSize = null)
    {
        Page = page is null or < 1 ? 1 : page.Value;
        PageSize = pageSize switch
        {
            null or < 1 => TamanhoPadraoDePagina,
            > TamanhoMaximoDePagina => TamanhoMaximoDePagina,
            _ => pageSize.Value
        };
    }

    public int Page { get; }

    public int PageSize { get; }

    public int QuantidadeParaPular() => (Page - 1) * PageSize;
}

/// <summary>Pagina de resultados devolvida pelas consultas (RNF0011).</summary>
public sealed record ResultadoPaginado<T>(
    IReadOnlyCollection<T> Itens,
    int Page,
    int PageSize,
    long Total)
{
    public int TotalDePaginas => PageSize == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}
