using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Domain.Common;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Financeiro;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ChamadosManutencao.Application.UC11Faturas;

/// <summary>Pagamento de fatura em aberto (RF0084).</summary>
public sealed record PagarFaturaCommand(Guid FormaPagamentoId);

/// <summary>Filtros de RF0083.</summary>
public sealed record FiltroDeFaturas(
    StatusFatura? Status = null,
    DateTimeOffset? DataInicio = null,
    DateTimeOffset? DataFim = null,
    long? NumeroChamado = null,
    Guid? ClienteId = null,
    int? Page = null,
    int? PageSize = null);

public sealed record FaturaDto(
    Guid Id,
    long Numero,
    Guid AtendimentoId,
    Guid ChamadoId,
    long NumeroDoChamado,
    Guid ClienteId,
    DateTimeOffset DataEmissao,
    DateTimeOffset DataVencimento,
    decimal ValorPecas,
    decimal ValorMaoObra,
    decimal TaxaDeslocamento,
    decimal ValorTotal,
    StatusFatura Status);

public sealed record PagamentoDto(
    Guid Id,
    Guid FaturaId,
    Guid FormaPagamentoId,
    DateTimeOffset DataHoraPagamento,
    decimal ValorPago,
    string IdentificadorTransacao,
    StatusPagamento Status);

public sealed class PagarFaturaValidator : AbstractValidator<PagarFaturaCommand>
{
    public PagarFaturaValidator() => RuleFor(p => p.FormaPagamentoId).NotEmpty();
}

/// <summary>
/// Consulta faturas do cliente ou de todos os clientes.
/// Requisitos: RF0083, RNF0011.
/// Caso de uso: UC11.
/// </summary>
public sealed class ConsultarFaturasHandler
{
    private readonly IContextoDeLeitura _leitura;
    private readonly IUsuarioAtual _usuarioAtual;

    public ConsultarFaturasHandler(IContextoDeLeitura leitura, IUsuarioAtual usuarioAtual)
    {
        _leitura = leitura;
        _usuarioAtual = usuarioAtual;
    }

    public Task<ResultadoPaginado<FaturaDto>> MinhasFaturasAsync(
        FiltroDeFaturas filtro,
        CancellationToken cancellationToken = default)
    {
        var clienteId = Autorizacao.ExigirAutenticado(_usuarioAtual);

        return ConsultarAsync(filtro with { ClienteId = clienteId }, cancellationToken);
    }

    public async Task<ResultadoPaginado<FaturaDto>> ConsultarAsync(
        FiltroDeFaturas filtro,
        CancellationToken cancellationToken = default)
    {
        var paginacao = new ParametrosDePaginacao(filtro.Page, filtro.PageSize);
        var consulta = _leitura.Faturas;

        if (filtro.Status is not null)
        {
            consulta = consulta.Where(f => f.Status == filtro.Status);
        }

        if (filtro.ClienteId is not null)
        {
            consulta = consulta.Where(f => f.ClienteId == filtro.ClienteId);
        }

        if (filtro.DataInicio is not null)
        {
            consulta = consulta.Where(f => f.DataEmissao >= filtro.DataInicio);
        }

        if (filtro.DataFim is not null)
        {
            consulta = consulta.Where(f => f.DataEmissao <= filtro.DataFim);
        }

        if (filtro.NumeroChamado is not null)
        {
            consulta = consulta.Where(f => _leitura.Chamados
                .Any(c => c.Id == f.ChamadoId && c.Numero == filtro.NumeroChamado));
        }

        var total = await consulta.LongCountAsync(cancellationToken);

        var itens = await consulta
            .OrderByDescending(f => f.DataEmissao)
            .Skip(paginacao.QuantidadeParaPular())
            .Take(paginacao.PageSize)
            .Select(f => new FaturaDto(
                f.Id,
                f.Numero,
                f.AtendimentoId,
                f.ChamadoId,
                _leitura.Chamados.Where(c => c.Id == f.ChamadoId).Select(c => c.Numero).First(),
                f.ClienteId,
                f.DataEmissao,
                f.DataVencimento,
                f.ValorPecas,
                f.ValorMaoObra,
                f.TaxaDeslocamento,
                f.ValorTotal,
                f.Status))
            .ToListAsync(cancellationToken);

        return new ResultadoPaginado<FaturaDto>(itens, paginacao.Page, paginacao.PageSize, total);
    }

    public async Task<FaturaDto> ObterAsync(Guid faturaId, CancellationToken cancellationToken = default)
    {
        var fatura = await _leitura.Faturas
            .Where(f => f.Id == faturaId)
            .Select(f => new FaturaDto(
                f.Id,
                f.Numero,
                f.AtendimentoId,
                f.ChamadoId,
                _leitura.Chamados.Where(c => c.Id == f.ChamadoId).Select(c => c.Numero).First(),
                f.ClienteId,
                f.DataEmissao,
                f.DataVencimento,
                f.ValorPecas,
                f.ValorMaoObra,
                f.TaxaDeslocamento,
                f.ValorTotal,
                f.Status))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Fatura", faturaId);

        Autorizacao.ExigirDonoOuAdministrador(_usuarioAtual, fatura.ClienteId);

        return fatura;
    }

    public async Task<IReadOnlyCollection<PagamentoDto>> ConsultarPagamentosAsync(
        Guid faturaId,
        CancellationToken cancellationToken = default)
    {
        var clienteId = await _leitura.Faturas
            .Where(f => f.Id == faturaId)
            .Select(f => (Guid?)f.ClienteId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Fatura", faturaId);

        Autorizacao.ExigirDonoOuAdministrador(_usuarioAtual, clienteId);

        return await _leitura.Pagamentos
            .Where(p => p.FaturaId == faturaId)
            .OrderBy(p => p.DataHoraPagamento)
            .Select(p => new PagamentoDto(
                p.Id,
                p.FaturaId,
                p.FormaPagamentoId,
                p.DataHoraPagamento,
                p.ValorPago,
                p.IdentificadorTransacao,
                p.Status))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
/// Registra o pagamento de uma fatura em aberto.
/// Requisitos: RF0084, RNF0061.
/// Caso de uso: UC11.
/// </summary>
public sealed class PagarFaturaHandler
{
    private readonly IFaturaRepositorio _faturas;
    private readonly IClienteRepositorio _clientes;
    private readonly IGatewayPagamento _gateway;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IGeradorId _geradorId;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public PagarFaturaHandler(
        IFaturaRepositorio faturas,
        IClienteRepositorio clientes,
        IGatewayPagamento gateway,
        IUsuarioAtual usuarioAtual,
        IGeradorId geradorId,
        IRelogio relogio,
        IUnitOfWork unidadeDeTrabalho)
    {
        _faturas = faturas;
        _clientes = clientes;
        _gateway = gateway;
        _usuarioAtual = usuarioAtual;
        _geradorId = geradorId;
        _relogio = relogio;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task<PagamentoDto> ExecutarAsync(
        Guid faturaId,
        PagarFaturaCommand comando,
        CancellationToken cancellationToken = default)
    {
        var fatura = await _faturas.ObterComPagamentosAsync(faturaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Fatura", faturaId);

        Autorizacao.ExigirDono(_usuarioAtual, fatura.ClienteId);

        var cliente = await _clientes.ObterComFormasPagamentoAsync(fatura.ClienteId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Cliente", fatura.ClienteId);

        var forma = cliente.FormasPagamento.SingleOrDefault(f => f.Id == comando.FormaPagamentoId && f.Ativa)
            ?? throw new RecursoNaoEncontradoException("Forma de pagamento", comando.FormaPagamentoId);

        // O token do cartao substitui o numero (RNF0061). PIX e boleto usam o identificador da
        // propria forma como referencia junto ao gateway simulado.
        var referencia = forma is CartaoCredito cartao ? cartao.TokenOperadora : forma.Id.ToString();

        var retorno = await _gateway.AutorizarTransacaoAsync(
            referencia,
            fatura.ValorTotal,
            cancellationToken);

        var pagamento = new Pagamento(
            _geradorId.NovoId(),
            fatura.Id,
            forma.Id,
            _relogio.Agora,
            fatura.ValorTotal,
            retorno.IdentificadorTransacao,
            retorno.Aprovada ? StatusPagamento.Aprovado : StatusPagamento.Recusado);

        fatura.RegistrarPagamento(pagamento);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        if (!retorno.Aprovada)
        {
            throw new ConflitoException(
                $"Pagamento recusado pela operadora ({retorno.CodigoDeRetorno}): {retorno.Mensagem}",
                "RF0084");
        }

        return new PagamentoDto(
            pagamento.Id,
            pagamento.FaturaId,
            pagamento.FormaPagamentoId,
            pagamento.DataHoraPagamento,
            pagamento.ValorPago,
            pagamento.IdentificadorTransacao,
            pagamento.Status);
    }
}
