using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Domain.Catalogo;
using ChamadosManutencao.Domain.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ChamadosManutencao.Application.UC03Catalogo;

/// <summary>Cadastro e alteracao de categoria (RF0031, RF0033).</summary>
public sealed record CategoriaCommand(
    string Nome,
    string Descricao,
    bool ExigeFoto,
    bool CategoriaDeRisco);

/// <summary>Cadastro e alteracao de tipo de servico (RF0032, RF0033).</summary>
public sealed record TipoServicoCommand(string Nome, string Descricao);

public sealed record CategoriaDto(
    Guid Id,
    string Nome,
    string Descricao,
    bool ExigeFoto,
    bool CategoriaDeRisco,
    bool Ativa);

public sealed record TipoServicoDto(
    Guid Id,
    Guid CategoriaServicoId,
    string Nome,
    string Descricao,
    bool Ativo);

public sealed class CategoriaValidator : AbstractValidator<CategoriaCommand>
{
    public CategoriaValidator()
    {
        RuleFor(c => c.Nome).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Descricao).NotEmpty().MaximumLength(400);
    }
}

public sealed class TipoServicoValidator : AbstractValidator<TipoServicoCommand>
{
    public TipoServicoValidator()
    {
        RuleFor(t => t.Nome).NotEmpty().MaximumLength(100);
        RuleFor(t => t.Descricao).NotEmpty().MaximumLength(400);
    }
}

/// <summary>
/// Mantem o catalogo de categorias e tipos de servico.
/// Requisitos: RF0031, RF0032, RF0033.
/// Caso de uso: UC03.
/// </summary>
public sealed class ManterCatalogoHandler
{
    private readonly ICatalogoRepositorio _catalogo;
    private readonly IContextoDeLeitura _leitura;
    private readonly IGeradorId _geradorId;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public ManterCatalogoHandler(
        ICatalogoRepositorio catalogo,
        IContextoDeLeitura leitura,
        IGeradorId geradorId,
        IUnitOfWork unidadeDeTrabalho)
    {
        _catalogo = catalogo;
        _leitura = leitura;
        _geradorId = geradorId;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task<CategoriaDto> CadastrarCategoriaAsync(
        CategoriaCommand comando,
        CancellationToken cancellationToken = default)
    {
        var jaExiste = await _leitura.CategoriasServico
            .AnyAsync(c => c.Nome.ToLower() == comando.Nome.Trim().ToLower(), cancellationToken);

        if (jaExiste)
        {
            throw new ConflitoException($"Ja existe a categoria de servico '{comando.Nome}'.", "RF0031");
        }

        var categoria = new CategoriaServico(
            _geradorId.NovoId(),
            comando.Nome,
            comando.Descricao,
            comando.ExigeFoto,
            comando.CategoriaDeRisco);

        _catalogo.AdicionarCategoria(categoria);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return ParaDto(categoria);
    }

    public async Task<CategoriaDto> AlterarCategoriaAsync(
        Guid categoriaId,
        CategoriaCommand comando,
        CancellationToken cancellationToken = default)
    {
        var categoria = await _catalogo.ObterCategoriaAsync(categoriaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Categoria de servico", categoriaId);

        categoria.Alterar(comando.Nome, comando.Descricao, comando.ExigeFoto, comando.CategoriaDeRisco);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return ParaDto(categoria);
    }

    public async Task<TipoServicoDto> CadastrarTipoServicoAsync(
        Guid categoriaId,
        TipoServicoCommand comando,
        CancellationToken cancellationToken = default)
    {
        var categoria = await _catalogo.ObterCategoriaComTiposAsync(categoriaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Categoria de servico", categoriaId);

        var tipo = categoria.AdicionarTipoServico(
            _geradorId.NovoId(),
            comando.Nome,
            comando.Descricao);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return ParaDto(tipo);
    }

    public async Task<TipoServicoDto> AlterarTipoServicoAsync(
        Guid tipoServicoId,
        TipoServicoCommand comando,
        CancellationToken cancellationToken = default)
    {
        var tipo = await _catalogo.ObterTipoServicoAsync(tipoServicoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Tipo de servico", tipoServicoId);

        tipo.Alterar(comando.Nome, comando.Descricao);

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return ParaDto(tipo);
    }

    private static CategoriaDto ParaDto(CategoriaServico categoria) => new(
        categoria.Id,
        categoria.Nome,
        categoria.Descricao,
        categoria.ExigeFoto,
        categoria.CategoriaDeRisco,
        categoria.Ativa);

    private static TipoServicoDto ParaDto(TipoServico tipo) => new(
        tipo.Id,
        tipo.CategoriaServicoId,
        tipo.Nome,
        tipo.Descricao,
        tipo.Ativo);
}

/// <summary>
/// Consulta o catalogo de categorias e tipos de servico.
/// Requisitos: RF0034, RNF0011.
/// Caso de uso: UC03.
/// </summary>
public sealed class ConsultarCatalogoHandler
{
    private readonly IContextoDeLeitura _leitura;

    public ConsultarCatalogoHandler(IContextoDeLeitura leitura) => _leitura = leitura;

    public async Task<IReadOnlyCollection<CategoriaDto>> ConsultarCategoriasAsync(
        bool? ativa,
        CancellationToken cancellationToken = default)
    {
        var consulta = _leitura.CategoriasServico;

        if (ativa is not null)
        {
            consulta = consulta.Where(c => c.Ativa == ativa);
        }

        var itens = await consulta
            .OrderBy(c => c.Nome)
            .Select(c => new CategoriaDto(
                c.Id,
                c.Nome,
                c.Descricao,
                c.ExigeFoto,
                c.CategoriaDeRisco,
                c.Ativa))
            .ToListAsync(cancellationToken);

        return itens;
    }

    public async Task<IReadOnlyCollection<TipoServicoDto>> ConsultarTiposDeServicoAsync(
        Guid? categoriaId,
        bool? ativo,
        CancellationToken cancellationToken = default)
    {
        var consulta = _leitura.TiposServico;

        if (categoriaId is not null)
        {
            consulta = consulta.Where(t => t.CategoriaServicoId == categoriaId);
        }

        if (ativo is not null)
        {
            consulta = consulta.Where(t => t.Ativo == ativo);
        }

        var itens = await consulta
            .OrderBy(t => t.Nome)
            .Select(t => new TipoServicoDto(
                t.Id,
                t.CategoriaServicoId,
                t.Nome,
                t.Descricao,
                t.Ativo))
            .ToListAsync(cancellationToken);

        return itens;
    }
}
