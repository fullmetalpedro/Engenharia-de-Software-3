using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Financeiro;

namespace ChamadosManutencao.Domain.Pessoas;

/// <summary>
/// Cliente do servico. Requisitos: RF0011, RN0011, RNF0023.
/// </summary>
public sealed class Cliente : Usuario
{
    private readonly List<Imovel> _imoveis = [];
    private readonly List<FormaPagamento> _formasPagamento = [];

    /// <remarks>
    /// RN0011 exige ao menos um imovel no cadastro. A lista de imoveis chega pronta da camada
    /// de aplicacao, que ja gerou os identificadores.
    /// </remarks>
    public Cliente(
        Guid id,
        string codigoCliente,
        string nomeCompleto,
        string cpf,
        string email,
        string telefone,
        string senhaHash,
        IEnumerable<Imovel> imoveis)
        : base(id, nomeCompleto, cpf, email, telefone, senhaHash, "RN0011")
    {
        CodigoCliente = Garantir.TextoComTamanhoMaximo(codigoCliente, 20, "codigo do cliente", "RNF0023");

        var lista = imoveis?.ToList() ?? [];

        if (lista.Count == 0)
        {
            throw new ExcecaoDeDominio(
                "O cadastro de cliente exige ao menos um imovel.",
                "RN0011");
        }

        _imoveis.AddRange(lista);
    }

    private Cliente()
    {
        CodigoCliente = null!;
    }

    /// <summary>Codigo unico sequencial no formato CLI-000001 (RNF0023).</summary>
    public string CodigoCliente { get; private set; }

    public override PapelUsuario Papel => PapelUsuario.Cliente;

    public IReadOnlyCollection<Imovel> Imoveis => _imoveis.AsReadOnly();

    public IReadOnlyCollection<FormaPagamento> FormasPagamento => _formasPagamento.AsReadOnly();

    /// <summary>RF0016.</summary>
    public void AdicionarImovel(Imovel imovel)
    {
        Garantir.NaoNulo(imovel, "imovel", "RN0012");
        _imoveis.Add(imovel);
    }

    /// <summary>
    /// RF0016. A remocao do ultimo imovel e bloqueada porque RN0011 exige ao menos um.
    /// </summary>
    public void RemoverImovel(Guid imovelId)
    {
        var imovel = _imoveis.SingleOrDefault(i => i.Id == imovelId)
            ?? throw new ExcecaoDeDominio("Imovel nao encontrado no cadastro do cliente.");

        if (_imoveis.Count == 1)
        {
            throw new ExcecaoDeDominio(
                "O cliente deve manter ao menos um imovel cadastrado.",
                "RN0011");
        }

        _imoveis.Remove(imovel);
    }

    /// <summary>RF0081.</summary>
    public void AdicionarFormaPagamento(FormaPagamento forma)
    {
        Garantir.NaoNulo(forma, "forma de pagamento", "RF0081");

        if (forma.Principal || _formasPagamento.Count == 0)
        {
            foreach (var outra in _formasPagamento)
            {
                outra.DeixarDeSerPrincipal();
            }

            forma.TornarPrincipal();
        }

        _formasPagamento.Add(forma);
    }

    /// <summary>RF0081: exatamente uma forma de pagamento ativa e principal por vez.</summary>
    public void DefinirFormaPagamentoPrincipal(Guid formaPagamentoId)
    {
        var escolhida = _formasPagamento.SingleOrDefault(f => f.Id == formaPagamentoId)
            ?? throw new ExcecaoDeDominio("Forma de pagamento nao encontrada.", "RF0081");

        if (!escolhida.Ativa)
        {
            throw new ExcecaoDeDominio(
                "Uma forma de pagamento inativa nao pode ser definida como principal.",
                "RF0081");
        }

        foreach (var forma in _formasPagamento)
        {
            forma.DeixarDeSerPrincipal();
        }

        escolhida.TornarPrincipal();
    }

    /// <summary>RF0081: a exclusao e logica, para preservar o historico de pagamentos.</summary>
    public void RemoverFormaPagamento(Guid formaPagamentoId)
    {
        var forma = _formasPagamento.SingleOrDefault(f => f.Id == formaPagamentoId)
            ?? throw new ExcecaoDeDominio("Forma de pagamento nao encontrada.", "RF0081");

        forma.Inativar();

        if (forma.Principal)
        {
            forma.DeixarDeSerPrincipal();

            var proxima = _formasPagamento.FirstOrDefault(f => f.Ativa);
            proxima?.TornarPrincipal();
        }
    }

    public bool PossuiImovel(Guid imovelId) => _imoveis.Any(i => i.Id == imovelId);
}
