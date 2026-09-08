using ChamadosManutencao.Domain.Atendimentos;
using ChamadosManutencao.Domain.Avaliacoes;
using ChamadosManutencao.Domain.Catalogo;
using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Financeiro;
using ChamadosManutencao.Domain.Pessoas;

namespace ChamadosManutencao.Application.Abstractions;

/// <summary>
/// Repositorios de escrita. As consultas de lista (RNF0011) usam projecao direta para DTO no
/// proprio handler, com AsNoTracking, e por isso nao passam por aqui.
/// </summary>
public interface IUsuarioRepositorio
{
    Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExisteCpfAsync(string cpf, Guid? ignorarId = null, CancellationToken cancellationToken = default);

    Task<bool> ExisteEmailAsync(string email, Guid? ignorarId = null, CancellationToken cancellationToken = default);

    Task<bool> ExisteAdministradorAsync(CancellationToken cancellationToken = default);

    void Adicionar(Usuario usuario);
}

public interface IClienteRepositorio
{
    Task<Cliente?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Cliente?> ObterComImoveisAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Cliente?> ObterComFormasPagamentoAsync(Guid id, CancellationToken cancellationToken = default);

    void Adicionar(Cliente cliente);
}

public interface ITecnicoRepositorio
{
    Task<Tecnico?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Carrega especialidades, areas e documentos, necessarios para RN0022 e RN0023.</summary>
    Task<Tecnico?> ObterCompletoAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>RN0041: janelas ja ocupadas pelo tecnico, exceto as de um chamado informado.</summary>
    Task<IReadOnlyCollection<JanelaDeAtendimento>> ObterCompromissosAsync(
        Guid tecnicoId,
        Guid? ignorarChamadoId = null,
        CancellationToken cancellationToken = default);

    void Adicionar(Tecnico tecnico);
}

public interface ICatalogoRepositorio
{
    Task<CategoriaServico?> ObterCategoriaAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CategoriaServico?> ObterCategoriaComTiposAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CategoriaServico>> ObterCategoriasPorIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default);

    Task<TipoServico?> ObterTipoServicoAsync(Guid id, CancellationToken cancellationToken = default);

    void AdicionarCategoria(CategoriaServico categoria);
}

public interface IChamadoRepositorio
{
    Task<Chamado?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Carrega anexos, historico e agendamentos do agregado.</summary>
    Task<Chamado?> ObterCompletoAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Chamado?> ObterPorAgendamentoAsync(Guid agendamentoId, CancellationToken cancellationToken = default);

    void Adicionar(Chamado chamado);
}

public interface IAtendimentoRepositorio
{
    Task<Atendimento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Atendimento?> ObterCompletoAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Atendimento?> ObterPorChamadoAsync(Guid chamadoId, CancellationToken cancellationToken = default);

    Task<Atendimento?> ObterPorOrcamentoAsync(Guid orcamentoId, CancellationToken cancellationToken = default);

    Task<Garantia?> ObterGarantiaAsync(Guid garantiaId, CancellationToken cancellationToken = default);

    Task<Garantia?> ObterGarantiaPorAtendimentoAsync(
        Guid atendimentoId,
        CancellationToken cancellationToken = default);

    /// <summary>RN0043: orcamentos pendentes com prazo vencido, usados pelo job de expiracao.</summary>
    Task<IReadOnlyCollection<Orcamento>> ObterOrcamentosVencidosAsync(
        DateTimeOffset limite,
        CancellationToken cancellationToken = default);

    void Adicionar(Atendimento atendimento);

    void AdicionarGarantia(Garantia garantia);
}

public interface IAvaliacaoRepositorio
{
    Task<Avaliacao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Avaliacao?> ObterPorChamadoAsync(Guid chamadoId, CancellationToken cancellationToken = default);

    void Adicionar(Avaliacao avaliacao);
}

public interface IFaturaRepositorio
{
    Task<Fatura?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Fatura?> ObterComPagamentosAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Fatura?> ObterPorAtendimentoAsync(Guid atendimentoId, CancellationToken cancellationToken = default);

    /// <summary>Faturas emitidas com vencimento no passado, usadas pelo job de vencimento.</summary>
    Task<IReadOnlyCollection<Fatura>> ObterVencidasAsync(
        DateTimeOffset limite,
        CancellationToken cancellationToken = default);

    void Adicionar(Fatura fatura);
}
