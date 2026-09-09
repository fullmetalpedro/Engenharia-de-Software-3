using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Domain.Chamados;
using Microsoft.EntityFrameworkCore;

namespace ChamadosManutencao.Application.UC04Chamados;

/// <summary>
/// Consultas de chamado compartilhadas por UC04 (visao do cliente) e UC05 (visao do
/// administrador). RNF0011: paginacao obrigatoria e projecao direta para DTO.
/// </summary>
public sealed class ConsultarChamadosHandler
{
    private readonly IContextoDeLeitura _leitura;
    private readonly IUsuarioAtual _usuarioAtual;

    public ConsultarChamadosHandler(IContextoDeLeitura leitura, IUsuarioAtual usuarioAtual)
    {
        _leitura = leitura;
        _usuarioAtual = usuarioAtual;
    }

    /// <summary>
    /// Consulta os chamados do proprio cliente autenticado.
    /// Requisitos: RF0043, RNF0011.
    /// Caso de uso: UC04.
    /// </summary>
    public Task<ResultadoPaginado<ChamadoResumoDto>> MeusChamadosAsync(
        FiltroDeChamados filtro,
        CancellationToken cancellationToken = default)
    {
        var clienteId = Autorizacao.ExigirAutenticado(_usuarioAtual);

        return ConsultarAsync(filtro with { ClienteId = clienteId }, cancellationToken);
    }

    /// <summary>
    /// Consulta todos os chamados da empresa.
    /// Requisitos: RF0044, RNF0011.
    /// Caso de uso: UC05.
    /// </summary>
    public async Task<ResultadoPaginado<ChamadoResumoDto>> ConsultarAsync(
        FiltroDeChamados filtro,
        CancellationToken cancellationToken = default)
    {
        var paginacao = new ParametrosDePaginacao(filtro.Page, filtro.PageSize);
        var consulta = _leitura.Chamados;

        if (filtro.Status is not null)
        {
            consulta = consulta.Where(c => c.Status == filtro.Status);
        }

        if (filtro.Urgencia is not null)
        {
            consulta = consulta.Where(c => c.Urgencia == filtro.Urgencia);
        }

        if (filtro.ClienteId is not null)
        {
            consulta = consulta.Where(c => c.ClienteId == filtro.ClienteId);
        }

        if (filtro.TecnicoId is not null)
        {
            consulta = consulta.Where(c => c.TecnicoId == filtro.TecnicoId);
        }

        if (filtro.CategoriaId is not null)
        {
            consulta = consulta.Where(c => _leitura.TiposServico
                .Any(t => t.Id == c.TipoServicoId && t.CategoriaServicoId == filtro.CategoriaId));
        }

        if (filtro.DataInicio is not null)
        {
            consulta = consulta.Where(c => c.DataHoraAbertura >= filtro.DataInicio);
        }

        if (filtro.DataFim is not null)
        {
            consulta = consulta.Where(c => c.DataHoraAbertura <= filtro.DataFim);
        }

        if (filtro.Numero is not null)
        {
            consulta = consulta.Where(c => c.Numero == filtro.Numero);
        }

        var total = await consulta.LongCountAsync(cancellationToken);

        var itens = await consulta
            .OrderByDescending(c => c.DataHoraAbertura)
            .Skip(paginacao.QuantidadeParaPular())
            .Take(paginacao.PageSize)
            .Select(c => new ChamadoResumoDto(
                c.Id,
                c.Numero,
                c.DataHoraAbertura,
                c.Status,
                c.Urgencia,
                c.ChamadoDeGarantia,
                c.ClienteId,
                c.ImovelId,
                c.TipoServicoId,
                _leitura.TiposServico.Where(t => t.Id == c.TipoServicoId).Select(t => t.Nome).First(),
                _leitura.TiposServico
                    .Where(t => t.Id == c.TipoServicoId)
                    .SelectMany(t => _leitura.CategoriasServico
                        .Where(cat => cat.Id == t.CategoriaServicoId)
                        .Select(cat => cat.Nome))
                    .First(),
                c.TecnicoId,
                c.TecnicoId == null
                    ? null
                    : _leitura.Tecnicos
                        .Where(t => t.Id == c.TecnicoId)
                        .Select(t => t.NomeCompleto)
                        .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new ResultadoPaginado<ChamadoResumoDto>(itens, paginacao.Page, paginacao.PageSize, total);
    }
}

/// <summary>
/// Consulta um chamado especifico.
/// Requisitos: RF0043, RF0044.
/// Casos de uso: UC04, UC05.
/// </summary>
public sealed class ObterChamadoHandler
{
    private readonly IContextoDeLeitura _leitura;
    private readonly IUsuarioAtual _usuarioAtual;

    public ObterChamadoHandler(IContextoDeLeitura leitura, IUsuarioAtual usuarioAtual)
    {
        _leitura = leitura;
        _usuarioAtual = usuarioAtual;
    }

    public async Task<ChamadoDetalheDto> ExecutarAsync(
        Guid chamadoId,
        CancellationToken cancellationToken = default)
    {
        var chamado = await _leitura.Chamados
            .Where(c => c.Id == chamadoId)
            .Select(c => new
            {
                c.Id,
                c.Numero,
                c.DataHoraAbertura,
                c.DescricaoProblema,
                c.Status,
                c.Urgencia,
                c.ChamadoDeGarantia,
                c.ClienteId,
                c.ImovelId,
                c.TipoServicoId,
                c.TecnicoId,
                c.ChamadoOriginalId,
                c.GarantiaId
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Chamado", chamadoId);

        // Cliente dono, tecnico atribuido ou administrador.
        var usuarioId = Autorizacao.ExigirAutenticado(_usuarioAtual);

        if (!Autorizacao.EhAdministrador(_usuarioAtual)
            && usuarioId != chamado.ClienteId
            && usuarioId != chamado.TecnicoId)
        {
            throw new AcessoNegadoException("Este chamado pertence a outro usuario.");
        }

        var tipo = await _leitura.TiposServico
            .Where(t => t.Id == chamado.TipoServicoId)
            .Select(t => new { t.Nome, t.CategoriaServicoId })
            .SingleAsync(cancellationToken);

        var categoria = await _leitura.CategoriasServico
            .Where(c => c.Id == tipo.CategoriaServicoId)
            .Select(c => c.Nome)
            .SingleAsync(cancellationToken);

        var tecnico = chamado.TecnicoId is null
            ? null
            : await _leitura.Tecnicos
                .Where(t => t.Id == chamado.TecnicoId)
                .Select(t => t.NomeCompleto)
                .SingleOrDefaultAsync(cancellationToken);

        var anexos = await _leitura.Anexos
            .Where(a => a.ChamadoId == chamadoId)
            .OrderBy(a => a.DataHoraUpload)
            .Select(a => new AnexoDto(
                a.Id,
                a.NomeArquivo,
                a.TipoMime,
                a.TamanhoBytes,
                a.DataHoraUpload,
                a.Origem))
            .ToListAsync(cancellationToken);

        var agendamentos = await _leitura.Agendamentos
            .Where(a => a.ChamadoId == chamadoId)
            .OrderBy(a => a.DataHoraProposta)
            .Select(a => new AgendamentoDto(
                a.Id,
                a.DataHoraProposta,
                a.DuracaoEmMinutos,
                a.DataHoraConfirmacao,
                a.OrigemProposta,
                a.Status))
            .ToListAsync(cancellationToken);

        return new ChamadoDetalheDto(
            chamado.Id,
            chamado.Numero,
            chamado.DataHoraAbertura,
            chamado.DescricaoProblema,
            chamado.Status,
            chamado.Urgencia,
            chamado.ChamadoDeGarantia,
            chamado.ClienteId,
            chamado.ImovelId,
            chamado.TipoServicoId,
            tipo.Nome,
            tipo.CategoriaServicoId,
            categoria,
            chamado.TecnicoId,
            tecnico,
            chamado.ChamadoOriginalId,
            chamado.GarantiaId,
            anexos,
            agendamentos);
    }
}

/// <summary>
/// Consulta o historico completo de mudancas de status.
/// Requisitos: RF0050.
/// Casos de uso: UC04, UC05.
/// </summary>
public sealed class ConsultarHistoricoDeStatusHandler
{
    private readonly IContextoDeLeitura _leitura;
    private readonly IUsuarioAtual _usuarioAtual;

    public ConsultarHistoricoDeStatusHandler(IContextoDeLeitura leitura, IUsuarioAtual usuarioAtual)
    {
        _leitura = leitura;
        _usuarioAtual = usuarioAtual;
    }

    public async Task<IReadOnlyCollection<HistoricoStatusDto>> ExecutarAsync(
        Guid chamadoId,
        CancellationToken cancellationToken = default)
    {
        var chamado = await _leitura.Chamados
            .Where(c => c.Id == chamadoId)
            .Select(c => new { c.ClienteId, c.TecnicoId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Chamado", chamadoId);

        var usuarioId = Autorizacao.ExigirAutenticado(_usuarioAtual);

        if (!Autorizacao.EhAdministrador(_usuarioAtual)
            && usuarioId != chamado.ClienteId
            && usuarioId != chamado.TecnicoId)
        {
            throw new AcessoNegadoException("Este chamado pertence a outro usuario.");
        }

        return await _leitura.HistoricosDeStatus
            .Where(h => h.ChamadoId == chamadoId)
            .OrderBy(h => h.DataHoraAlteracao)
            .Select(h => new HistoricoStatusDto(
                h.Id,
                h.StatusAnterior,
                h.StatusNovo,
                h.DataHoraAlteracao,
                h.UsuarioResponsavelId,
                h.Observacao))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
/// Consulta os agendamentos de um chamado.
/// Requisitos: RF0051.
/// Caso de uso: UC06.
/// </summary>
public sealed class ConsultarAgendamentosHandler
{
    private readonly IContextoDeLeitura _leitura;
    private readonly IUsuarioAtual _usuarioAtual;

    public ConsultarAgendamentosHandler(IContextoDeLeitura leitura, IUsuarioAtual usuarioAtual)
    {
        _leitura = leitura;
        _usuarioAtual = usuarioAtual;
    }

    public async Task<IReadOnlyCollection<AgendamentoDto>> ExecutarAsync(
        Guid chamadoId,
        CancellationToken cancellationToken = default)
    {
        var chamado = await _leitura.Chamados
            .Where(c => c.Id == chamadoId)
            .Select(c => new { c.ClienteId, c.TecnicoId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Chamado", chamadoId);

        var usuarioId = Autorizacao.ExigirAutenticado(_usuarioAtual);

        if (!Autorizacao.EhAdministrador(_usuarioAtual)
            && usuarioId != chamado.ClienteId
            && usuarioId != chamado.TecnicoId)
        {
            throw new AcessoNegadoException("Este chamado pertence a outro usuario.");
        }

        return await _leitura.Agendamentos
            .Where(a => a.ChamadoId == chamadoId)
            .OrderBy(a => a.DataHoraProposta)
            .Select(a => new AgendamentoDto(
                a.Id,
                a.DataHoraProposta,
                a.DuracaoEmMinutos,
                a.DataHoraConfirmacao,
                a.OrigemProposta,
                a.Status))
            .ToListAsync(cancellationToken);
    }
}
