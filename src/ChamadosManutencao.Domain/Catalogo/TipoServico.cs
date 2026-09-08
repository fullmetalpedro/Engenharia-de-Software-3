using ChamadosManutencao.Domain.Common;

namespace ChamadosManutencao.Domain.Catalogo;

/// <summary>
/// Tipo de servico vinculado a uma categoria ("troca de disjuntor" em "eletrica").
/// Requisitos: RF0032, RF0033, RF0034.
/// </summary>
public sealed class TipoServico : Entidade
{
    public TipoServico(Guid id, Guid categoriaServicoId, string nome, string descricao)
        : base(id)
    {
        CategoriaServicoId = categoriaServicoId;
        Nome = Garantir.TextoComTamanhoMaximo(nome, 100, "nome do tipo de servico", "RF0032");
        Descricao = Garantir.TextoComTamanhoMaximo(descricao, 400, "descricao do tipo de servico", "RF0032");
        Ativo = true;
    }

    private TipoServico()
    {
        Nome = null!;
        Descricao = null!;
    }

    public Guid CategoriaServicoId { get; private set; }

    public string Nome { get; private set; }

    public string Descricao { get; private set; }

    public bool Ativo { get; private set; }

    /// <summary>RF0033.</summary>
    public void Alterar(string nome, string descricao)
    {
        Nome = Garantir.TextoComTamanhoMaximo(nome, 100, "nome do tipo de servico", "RF0033");
        Descricao = Garantir.TextoComTamanhoMaximo(descricao, 400, "descricao do tipo de servico", "RF0033");
    }

    public void Ativar() => Ativo = true;

    public void Inativar() => Ativo = false;
}
