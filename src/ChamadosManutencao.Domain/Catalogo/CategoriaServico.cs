using ChamadosManutencao.Domain.Common;

namespace ChamadosManutencao.Domain.Catalogo;

/// <summary>
/// Categoria de servico (eletrica, hidraulica, ar-condicionado, eletrodomesticos).
/// Requisitos: RF0031, RF0033, RF0034, RN0031, RN0032.
/// </summary>
public sealed class CategoriaServico : RaizDeAgregado
{
    private readonly List<TipoServico> _tiposServico = [];

    public CategoriaServico(
        Guid id,
        string nome,
        string descricao,
        bool exigeFoto,
        bool categoriaDeRisco)
        : base(id)
    {
        Nome = Garantir.TextoComTamanhoMaximo(nome, 100, "nome da categoria", "RF0031");
        Descricao = Garantir.TextoComTamanhoMaximo(descricao, 400, "descricao da categoria", "RF0031");
        ExigeFoto = exigeFoto;
        CategoriaDeRisco = categoriaDeRisco;
        Ativa = true;
    }

    private CategoriaServico()
    {
        Nome = null!;
        Descricao = null!;
    }

    public string Nome { get; private set; }

    public string Descricao { get; private set; }

    /// <summary>RN0032: abrir chamado desta categoria exige ao menos uma foto.</summary>
    public bool ExigeFoto { get; private set; }

    /// <summary>RN0031: chamado desta categoria com indicacao de risco recebe urgencia Alta.</summary>
    public bool CategoriaDeRisco { get; private set; }

    public bool Ativa { get; private set; }

    public IReadOnlyCollection<TipoServico> TiposServico => _tiposServico.AsReadOnly();

    /// <summary>RF0033.</summary>
    public void Alterar(string nome, string descricao, bool exigeFoto, bool categoriaDeRisco)
    {
        Nome = Garantir.TextoComTamanhoMaximo(nome, 100, "nome da categoria", "RF0033");
        Descricao = Garantir.TextoComTamanhoMaximo(descricao, 400, "descricao da categoria", "RF0033");
        ExigeFoto = exigeFoto;
        CategoriaDeRisco = categoriaDeRisco;
    }

    public void Ativar() => Ativa = true;

    public void Inativar() => Ativa = false;

    /// <summary>RF0032.</summary>
    public TipoServico AdicionarTipoServico(Guid id, string nome, string descricao)
    {
        if (!Ativa)
        {
            throw new ExcecaoDeDominio(
                "Nao e possivel cadastrar tipo de servico em categoria inativa.",
                "RF0032");
        }

        if (_tiposServico.Any(t => string.Equals(t.Nome, nome?.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new ExcecaoDeDominio(
                $"Ja existe um tipo de servico chamado '{nome}' nesta categoria.",
                "RF0032");
        }

        var tipo = new TipoServico(id, Id, nome!, descricao);
        _tiposServico.Add(tipo);
        return tipo;
    }
}
