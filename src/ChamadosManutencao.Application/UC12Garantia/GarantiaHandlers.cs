using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Application.UC07Atendimento;
using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ChamadosManutencao.Application.UC12Garantia;

/// <summary>Acionamento da garantia (RF0085).</summary>
/// <summary>
/// Acionamento da garantia (RF0085). O tipo de servico e opcional: sem ele o chamado de
/// garantia herda o do atendimento original. Informado e diferente, a RN0072 recusa — o
/// defeito e de outra natureza e nao esta coberto por esta garantia.
/// </summary>
public sealed record AcionarGarantiaCommand(string DescricaoProblema, Guid? TipoServicoId = null);

/// <summary>
/// Resposta do acionamento. Quando o tecnico original nao serve mais, o chamado nasce sem
/// tecnico e o motivo vem explicito.
/// </summary>
public sealed record ResultadoDoAcionamentoDto(
    Guid ChamadoId,
    long NumeroDoChamado,
    StatusChamado Status,
    Guid ChamadoOriginalId,
    Guid GarantiaId,
    Guid? TecnicoId,
    bool AtribuidoAoTecnicoOriginal,
    string? MotivoDaTriagemManual);

public sealed class AcionarGarantiaValidator : AbstractValidator<AcionarGarantiaCommand>
{
    public AcionarGarantiaValidator() =>
        RuleFor(g => g.DescricaoProblema).NotEmpty().MaximumLength(2000);
}

/// <summary>
/// Aciona a garantia de um atendimento concluido, gerando um chamado de garantia.
/// Requisitos: RF0085, RF0041, RF0047, RN0022, RN0023, RN0072, RNF0042.
/// Caso de uso: UC12.
/// </summary>
public sealed class AcionarGarantiaHandler
{
    private readonly IAtendimentoRepositorio _atendimentos;
    private readonly IChamadoRepositorio _chamados;
    private readonly IFaturaRepositorio _faturas;
    private readonly ITecnicoRepositorio _tecnicos;
    private readonly IContextoDeLeitura _leitura;
    private readonly IGeradorDeSequencias _sequencias;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IGeradorId _geradorId;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public AcionarGarantiaHandler(
        IAtendimentoRepositorio atendimentos,
        IChamadoRepositorio chamados,
        IFaturaRepositorio faturas,
        ITecnicoRepositorio tecnicos,
        IContextoDeLeitura leitura,
        IGeradorDeSequencias sequencias,
        IUsuarioAtual usuarioAtual,
        IGeradorId geradorId,
        IRelogio relogio,
        IUnitOfWork unidadeDeTrabalho)
    {
        _atendimentos = atendimentos;
        _chamados = chamados;
        _faturas = faturas;
        _tecnicos = tecnicos;
        _leitura = leitura;
        _sequencias = sequencias;
        _usuarioAtual = usuarioAtual;
        _geradorId = geradorId;
        _relogio = relogio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task<ResultadoDoAcionamentoDto> ExecutarAsync(
        Guid atendimentoId,
        AcionarGarantiaCommand comando,
        CancellationToken cancellationToken = default)
    {
        var atendimento = await _atendimentos.ObterPorIdAsync(atendimentoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Atendimento", atendimentoId);

        var chamadoOriginal = await _chamados.ObterPorIdAsync(atendimento.ChamadoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Chamado", atendimento.ChamadoId);

        Autorizacao.ExigirDono(_usuarioAtual, chamadoOriginal.ClienteId);

        var garantia = await _atendimentos.ObterGarantiaPorAtendimentoAsync(atendimentoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Garantia do atendimento", atendimentoId);

        // RN0072 com a decisao D12: chamado de garantia nao gera fatura, entao a checagem de
        // quitacao recai sobre a fatura do atendimento original.
        var faturaQuitada = await FaturaEstaQuitadaAsync(atendimento.Id, chamadoOriginal, cancellationToken);

        var tipoServicoSolicitado = comando.TipoServicoId ?? chamadoOriginal.TipoServicoId;

        var motivo = garantia.MotivoDeRecusa(
            _relogio.Agora,
            faturaQuitada,
            chamadoOriginal.TipoServicoId,
            tipoServicoSolicitado);

        if (motivo is not null)
        {
            throw new ConflitoException($"Garantia nao pode ser acionada. {motivo}", "RN0072");
        }

        var numero = await _sequencias.ProximoNumeroDeChamadoAsync(cancellationToken);

        var chamadoDeGarantia = Chamado.AbrirParaGarantia(
            _geradorId.NovoId(),
            numero,
            chamadoOriginal.ClienteId,
            chamadoOriginal.ImovelId,
            chamadoOriginal.TipoServicoId,
            comando.DescricaoProblema,
            _relogio.Agora,
            chamadoOriginal.Id,
            garantia.Id,
            _geradorId);

        _chamados.Adicionar(chamadoDeGarantia);

        // RF0085: atribuir preferencialmente o mesmo tecnico do atendimento original.
        var (atribuido, motivoDaTriagem) = await TentarAtribuirTecnicoOriginalAsync(
            chamadoDeGarantia,
            atendimento.TecnicoId,
            chamadoOriginal.ImovelId,
            chamadoOriginal.TipoServicoId,
            cancellationToken);

        if (!atribuido)
        {
            // Sem tecnico, o chamado vai para EM ANALISE e aguarda triagem manual.
            chamadoDeGarantia.AlterarStatus(
                StatusChamado.EmAnalise,
                chamadoOriginal.ClienteId,
                $"Chamado de garantia em triagem manual: {motivoDaTriagem}",
                _relogio.Agora,
                _geradorId);
        }

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return new ResultadoDoAcionamentoDto(
            chamadoDeGarantia.Id,
            chamadoDeGarantia.Numero,
            chamadoDeGarantia.Status,
            chamadoOriginal.Id,
            garantia.Id,
            chamadoDeGarantia.TecnicoId,
            atribuido,
            motivoDaTriagem);
    }

    private async Task<bool> FaturaEstaQuitadaAsync(
        Guid atendimentoId,
        Chamado chamadoOriginal,
        CancellationToken cancellationToken)
    {
        var fatura = await _faturas.ObterPorAtendimentoAsync(atendimentoId, cancellationToken);

        if (fatura is not null)
        {
            return fatura.EstaQuitada();
        }

        // Decisao D12: atendimento de garantia nao gera fatura; olha-se a fatura do chamado
        // original que deu origem a cadeia.
        if (!chamadoOriginal.ChamadoDeGarantia || chamadoOriginal.ChamadoOriginalId is null)
        {
            return false;
        }

        var faturaOriginal = await _leitura.Faturas
            .Where(f => f.ChamadoId == chamadoOriginal.ChamadoOriginalId)
            .Select(f => f.Status)
            .FirstOrDefaultAsync(cancellationToken);

        return faturaOriginal == StatusFatura.Paga;
    }

    private async Task<(bool Atribuido, string? Motivo)> TentarAtribuirTecnicoOriginalAsync(
        Chamado chamadoDeGarantia,
        Guid tecnicoOriginalId,
        Guid imovelId,
        Guid tipoServicoId,
        CancellationToken cancellationToken)
    {
        var tecnico = await _tecnicos.ObterCompletoAsync(tecnicoOriginalId, cancellationToken);

        if (tecnico is null)
        {
            return (false, "o tecnico original nao foi encontrado.");
        }

        if (!tecnico.Ativo)
        {
            return (false, "o tecnico original esta inativo.");
        }

        var categoriaId = await _leitura.TiposServico
            .Where(t => t.Id == tipoServicoId)
            .Select(t => t.CategoriaServicoId)
            .SingleAsync(cancellationToken);

        var cep = await _leitura.Imoveis
            .Where(i => i.Id == imovelId)
            .Select(i => i.Cep)
            .SingleAsync(cancellationToken);

        if (!tecnico.AtendeCategoria(categoriaId))
        {
            return (false, "o tecnico original nao atende mais a categoria do servico.");
        }

        if (!tecnico.AtendeCep(cep))
        {
            return (false, "o tecnico original nao atende mais a area do imovel.");
        }

        chamadoDeGarantia.AtribuirTecnico(
            tecnico,
            categoriaId,
            cep,
            chamadoDeGarantia.ClienteId,
            _relogio.Agora,
            _geradorId);

        return (true, null);
    }
}

/// <summary>
/// Consulta a garantia de um atendimento.
/// Requisitos: RF0085, RN0072.
/// Caso de uso: UC12.
/// </summary>
public sealed class ConsultarGarantiaHandler
{
    private readonly IAtendimentoRepositorio _atendimentos;
    private readonly IChamadoRepositorio _chamados;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IRelogio _relogio;

    public ConsultarGarantiaHandler(
        IAtendimentoRepositorio atendimentos,
        IChamadoRepositorio chamados,
        IUsuarioAtual usuarioAtual,
        IRelogio relogio)
    {
        _atendimentos = atendimentos;
        _chamados = chamados;
        _usuarioAtual = usuarioAtual;
        _relogio = relogio;
    }

    public async Task<GarantiaDto> ExecutarAsync(
        Guid atendimentoId,
        CancellationToken cancellationToken = default)
    {
        var atendimento = await _atendimentos.ObterPorIdAsync(atendimentoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Atendimento", atendimentoId);

        var chamado = await _chamados.ObterPorIdAsync(atendimento.ChamadoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Chamado", atendimento.ChamadoId);

        Autorizacao.ExigirDonoOuAdministrador(_usuarioAtual, chamado.ClienteId);

        var garantia = await _atendimentos.ObterGarantiaPorAtendimentoAsync(atendimentoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Garantia do atendimento", atendimentoId);

        return new GarantiaDto(
            garantia.Id,
            garantia.AtendimentoId,
            garantia.DataInicio,
            garantia.DataFim,
            garantia.PrazoDias,
            garantia.EstaVigente(_relogio.Agora));
    }
}
