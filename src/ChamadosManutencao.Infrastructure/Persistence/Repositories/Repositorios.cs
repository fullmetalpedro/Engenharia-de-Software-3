using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Domain.Atendimentos;
using ChamadosManutencao.Domain.Avaliacoes;
using ChamadosManutencao.Domain.Catalogo;
using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Financeiro;
using ChamadosManutencao.Domain.Pessoas;
using Microsoft.EntityFrameworkCore;

namespace ChamadosManutencao.Infrastructure.Persistence.Repositories;

/// <summary>Repositorio de usuarios, usado pela autenticacao e pelas validacoes de unicidade.</summary>
public sealed class UsuarioRepositorio : IUsuarioRepositorio
{
    private readonly AppDbContext _contexto;

    public UsuarioRepositorio(AppDbContext contexto) => _contexto = contexto;

    public Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _contexto.Usuarios.SingleOrDefaultAsync(
            u => u.Email == email.ToLower(),
            cancellationToken);

    public Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _contexto.Usuarios.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<bool> ExisteCpfAsync(
        string cpf,
        Guid? ignorarId = null,
        CancellationToken cancellationToken = default) =>
        _contexto.Usuarios
            .AsNoTracking()
            .AnyAsync(u => u.Cpf == cpf && (ignorarId == null || u.Id != ignorarId), cancellationToken);

    public Task<bool> ExisteEmailAsync(
        string email,
        Guid? ignorarId = null,
        CancellationToken cancellationToken = default) =>
        _contexto.Usuarios
            .AsNoTracking()
            .AnyAsync(
                u => u.Email == email.ToLower() && (ignorarId == null || u.Id != ignorarId),
                cancellationToken);

    public Task<bool> ExisteAdministradorAsync(CancellationToken cancellationToken = default) =>
        _contexto.Administradores.AsNoTracking().AnyAsync(cancellationToken);

    public void Adicionar(Usuario usuario) => _contexto.Usuarios.Add(usuario);
}

/// <summary>Repositorio do agregado Cliente (UC01, UC10).</summary>
public sealed class ClienteRepositorio : IClienteRepositorio
{
    private readonly AppDbContext _contexto;

    public ClienteRepositorio(AppDbContext contexto) => _contexto = contexto;

    public Task<Cliente?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _contexto.Clientes.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Cliente?> ObterComImoveisAsync(Guid id, CancellationToken cancellationToken = default) =>
        _contexto.Clientes
            .Include("_imoveis")
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Cliente?> ObterComFormasPagamentoAsync(Guid id, CancellationToken cancellationToken = default) =>
        _contexto.Clientes
            .Include("_formasPagamento")
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public void Adicionar(Cliente cliente) => _contexto.Clientes.Add(cliente);
}

/// <summary>Repositorio do agregado Tecnico (UC02, UC05, UC06).</summary>
public sealed class TecnicoRepositorio : ITecnicoRepositorio
{
    private readonly AppDbContext _contexto;

    public TecnicoRepositorio(AppDbContext contexto) => _contexto = contexto;

    public Task<Tecnico?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _contexto.Tecnicos.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<Tecnico?> ObterCompletoAsync(Guid id, CancellationToken cancellationToken = default) =>
        _contexto.Tecnicos
            .Include("_especialidades")
            .Include("_areasAtendimento")
            .Include("_documentos")
            .SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    /// <summary>RN0041: janelas ocupadas pelos agendamentos vigentes do tecnico.</summary>
    public async Task<IReadOnlyCollection<JanelaDeAtendimento>> ObterCompromissosAsync(
        Guid tecnicoId,
        Guid? ignorarChamadoId = null,
        CancellationToken cancellationToken = default)
    {
        var consulta =
            from agendamento in _contexto.Agendamentos.AsNoTracking()
            join chamado in _contexto.Chamados.AsNoTracking()
                on agendamento.ChamadoId equals chamado.Id
            where chamado.TecnicoId == tecnicoId
                && (agendamento.Status == StatusAgendamento.Proposto
                    || agendamento.Status == StatusAgendamento.Confirmado)
                && (ignorarChamadoId == null || chamado.Id != ignorarChamadoId)
            select new { agendamento.DataHoraProposta, agendamento.DuracaoEmMinutos };

        var linhas = await consulta.ToListAsync(cancellationToken);

        return linhas
            .Select(l => new JanelaDeAtendimento(
                l.DataHoraProposta,
                l.DataHoraProposta.AddMinutes(l.DuracaoEmMinutos)))
            .ToList();
    }

    public void Adicionar(Tecnico tecnico) => _contexto.Tecnicos.Add(tecnico);
}

/// <summary>Repositorio do catalogo de servicos (UC03).</summary>
public sealed class CatalogoRepositorio : ICatalogoRepositorio
{
    private readonly AppDbContext _contexto;

    public CatalogoRepositorio(AppDbContext contexto) => _contexto = contexto;

    public Task<CategoriaServico?> ObterCategoriaAsync(Guid id, CancellationToken cancellationToken = default) =>
        _contexto.CategoriasServico.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<CategoriaServico?> ObterCategoriaComTiposAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        _contexto.CategoriasServico
            .Include("_tiposServico")
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<CategoriaServico>> ObterCategoriasPorIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var lista = ids.Distinct().ToList();

        return await _contexto.CategoriasServico
            .Where(c => lista.Contains(c.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<TipoServico?> ObterTipoServicoAsync(Guid id, CancellationToken cancellationToken = default) =>
        _contexto.TiposServico.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public void AdicionarCategoria(CategoriaServico categoria) =>
        _contexto.CategoriasServico.Add(categoria);
}

/// <summary>Repositorio do agregado Chamado (UC04 a UC07, UC12).</summary>
public sealed class ChamadoRepositorio : IChamadoRepositorio
{
    private readonly AppDbContext _contexto;

    public ChamadoRepositorio(AppDbContext contexto) => _contexto = contexto;

    public Task<Chamado?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _contexto.Chamados.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Chamado?> ObterCompletoAsync(Guid id, CancellationToken cancellationToken = default) =>
        _contexto.Chamados
            .Include("_anexos")
            .Include("_historicoStatus")
            .Include("_agendamentos")
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<Chamado?> ObterPorAgendamentoAsync(
        Guid agendamentoId,
        CancellationToken cancellationToken = default)
    {
        var chamadoId = await _contexto.Agendamentos
            .AsNoTracking()
            .Where(a => a.Id == agendamentoId)
            .Select(a => (Guid?)a.ChamadoId)
            .SingleOrDefaultAsync(cancellationToken);

        return chamadoId is null
            ? null
            : await ObterCompletoAsync(chamadoId.Value, cancellationToken);
    }

    public void Adicionar(Chamado chamado) => _contexto.Chamados.Add(chamado);
}

/// <summary>Repositorio do agregado Atendimento (UC07, UC12).</summary>
public sealed class AtendimentoRepositorio : IAtendimentoRepositorio
{
    private readonly AppDbContext _contexto;

    public AtendimentoRepositorio(AppDbContext contexto) => _contexto = contexto;

    public Task<Atendimento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _contexto.Atendimentos.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<Atendimento?> ObterCompletoAsync(Guid id, CancellationToken cancellationToken = default) =>
        _contexto.Atendimentos
            .Include("_orcamentos._itens")
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<Atendimento?> ObterPorChamadoAsync(
        Guid chamadoId,
        CancellationToken cancellationToken = default) =>
        _contexto.Atendimentos
            .Include("_orcamentos._itens")
            .SingleOrDefaultAsync(a => a.ChamadoId == chamadoId, cancellationToken);

    public async Task<Atendimento?> ObterPorOrcamentoAsync(
        Guid orcamentoId,
        CancellationToken cancellationToken = default)
    {
        var atendimentoId = await _contexto.Orcamentos
            .AsNoTracking()
            .Where(o => o.Id == orcamentoId)
            .Select(o => (Guid?)o.AtendimentoId)
            .SingleOrDefaultAsync(cancellationToken);

        return atendimentoId is null
            ? null
            : await ObterCompletoAsync(atendimentoId.Value, cancellationToken);
    }

    public Task<Garantia?> ObterGarantiaAsync(Guid garantiaId, CancellationToken cancellationToken = default) =>
        _contexto.Garantias.SingleOrDefaultAsync(g => g.Id == garantiaId, cancellationToken);

    public Task<Garantia?> ObterGarantiaPorAtendimentoAsync(
        Guid atendimentoId,
        CancellationToken cancellationToken = default) =>
        _contexto.Garantias.SingleOrDefaultAsync(g => g.AtendimentoId == atendimentoId, cancellationToken);

    public async Task<IReadOnlyCollection<Orcamento>> ObterOrcamentosVencidosAsync(
        DateTimeOffset limite,
        CancellationToken cancellationToken = default) =>
        await _contexto.Orcamentos
            .Where(o => o.Status == StatusOrcamento.Pendente && o.PrazoAprovacao < limite)
            .ToListAsync(cancellationToken);

    public void Adicionar(Atendimento atendimento) => _contexto.Atendimentos.Add(atendimento);

    public void AdicionarGarantia(Garantia garantia) => _contexto.Garantias.Add(garantia);
}

/// <summary>Repositorio do agregado Avaliacao (UC08).</summary>
public sealed class AvaliacaoRepositorio : IAvaliacaoRepositorio
{
    private readonly AppDbContext _contexto;

    public AvaliacaoRepositorio(AppDbContext contexto) => _contexto = contexto;

    public Task<Avaliacao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _contexto.Avaliacoes
            .Include(a => a.Resposta)
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<Avaliacao?> ObterPorChamadoAsync(Guid chamadoId, CancellationToken cancellationToken = default) =>
        _contexto.Avaliacoes
            .Include(a => a.Resposta)
            .SingleOrDefaultAsync(a => a.ChamadoId == chamadoId, cancellationToken);

    public void Adicionar(Avaliacao avaliacao) => _contexto.Avaliacoes.Add(avaliacao);
}

/// <summary>Repositorio do agregado Fatura (UC11).</summary>
public sealed class FaturaRepositorio : IFaturaRepositorio
{
    private readonly AppDbContext _contexto;

    public FaturaRepositorio(AppDbContext contexto) => _contexto = contexto;

    public Task<Fatura?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _contexto.Faturas.SingleOrDefaultAsync(f => f.Id == id, cancellationToken);

    public Task<Fatura?> ObterComPagamentosAsync(Guid id, CancellationToken cancellationToken = default) =>
        _contexto.Faturas
            .Include("_pagamentos")
            .SingleOrDefaultAsync(f => f.Id == id, cancellationToken);

    public Task<Fatura?> ObterPorAtendimentoAsync(
        Guid atendimentoId,
        CancellationToken cancellationToken = default) =>
        _contexto.Faturas
            .Include("_pagamentos")
            .SingleOrDefaultAsync(f => f.AtendimentoId == atendimentoId, cancellationToken);

    public async Task<IReadOnlyCollection<Fatura>> ObterVencidasAsync(
        DateTimeOffset limite,
        CancellationToken cancellationToken = default) =>
        await _contexto.Faturas
            .Where(f => f.Status == StatusFatura.Emitida && f.DataVencimento < limite)
            .ToListAsync(cancellationToken);

    public void Adicionar(Fatura fatura) => _contexto.Faturas.Add(fatura);
}
