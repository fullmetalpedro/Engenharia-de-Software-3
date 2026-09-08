using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Domain.Common;
using FluentValidation;

namespace ChamadosManutencao.Application.Autenticacao;

/// <summary>Credenciais do login.</summary>
public sealed record LoginCommand(string Email, string Senha);

/// <summary>Resposta do login e do refresh.</summary>
public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiraEm, string Papel);

/// <summary>Troca de senha do usuario autenticado.</summary>
public sealed record AlterarSenhaCommand(string SenhaAtual, string NovaSenha);

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
        RuleFor(c => c.Senha).NotEmpty();
    }
}

public sealed class AlterarSenhaValidator : AbstractValidator<AlterarSenhaCommand>
{
    public AlterarSenhaValidator()
    {
        RuleFor(c => c.SenhaAtual).NotEmpty();
        RuleFor(c => c.NovaSenha).NotEmpty();
    }
}

/// <summary>
/// Autentica o usuario e emite o token.
/// Requisitos: RNF0022.
/// Caso de uso: transversal (autenticacao).
/// </summary>
public sealed class LoginHandler
{
    private readonly IUsuarioRepositorio _usuarios;
    private readonly IServicoDeHashDeSenha _hash;
    private readonly IGeradorDeToken _token;

    public LoginHandler(
        IUsuarioRepositorio usuarios,
        IServicoDeHashDeSenha hash,
        IGeradorDeToken token)
    {
        _usuarios = usuarios;
        _hash = hash;
        _token = token;
    }

    public async Task<TokenResponse> ExecutarAsync(
        LoginCommand comando,
        CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarios.ObterPorEmailAsync(comando.Email, cancellationToken);

        if (usuario is null || !usuario.Autenticar(comando.Senha, _hash))
        {
            // Mensagem unica para credencial invalida e usuario inativo: nao revela qual dos
            // dois falhou.
            throw new AcessoNegadoException("Credenciais invalidas.");
        }

        var emitido = _token.Emitir(usuario);

        return new TokenResponse(emitido.AccessToken, emitido.ExpiraEm, emitido.Papel);
    }
}

/// <summary>
/// Reemite o token do usuario autenticado (decisao D14: sem entidade de refresh token).
/// Caso de uso: transversal (autenticacao).
/// </summary>
public sealed class RefreshHandler
{
    private readonly IUsuarioRepositorio _usuarios;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IGeradorDeToken _token;

    public RefreshHandler(
        IUsuarioRepositorio usuarios,
        IUsuarioAtual usuarioAtual,
        IGeradorDeToken token)
    {
        _usuarios = usuarios;
        _usuarioAtual = usuarioAtual;
        _token = token;
    }

    public async Task<TokenResponse> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        if (!_usuarioAtual.EstaAutenticado || _usuarioAtual.Id is null)
        {
            throw new AcessoNegadoException("Usuario nao autenticado.");
        }

        var usuario = await _usuarios.ObterPorIdAsync(_usuarioAtual.Id.Value, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Usuario", _usuarioAtual.Id.Value);

        if (!usuario.Ativo)
        {
            throw new AcessoNegadoException("Usuario inativo.");
        }

        var emitido = _token.Emitir(usuario);

        return new TokenResponse(emitido.AccessToken, emitido.ExpiraEm, emitido.Papel);
    }
}

/// <summary>
/// Troca a senha do usuario autenticado.
/// Requisitos: RNF0021, RNF0022.
/// Caso de uso: transversal (autenticacao).
/// </summary>
public sealed class AlterarSenhaHandler
{
    private readonly IUsuarioRepositorio _usuarios;
    private readonly IUsuarioAtual _usuarioAtual;
    private readonly IServicoDeHashDeSenha _hash;
    private readonly IPoliticaDeSenha _politica;
    private readonly IUnitOfWork _unidadeDeTrabalho;

    public AlterarSenhaHandler(
        IUsuarioRepositorio usuarios,
        IUsuarioAtual usuarioAtual,
        IServicoDeHashDeSenha hash,
        IPoliticaDeSenha politica,
        IUnitOfWork unidadeDeTrabalho)
    {
        _usuarios = usuarios;
        _usuarioAtual = usuarioAtual;
        _hash = hash;
        _politica = politica;
        _unidadeDeTrabalho = unidadeDeTrabalho;
    }

    public async Task ExecutarAsync(
        AlterarSenhaCommand comando,
        CancellationToken cancellationToken = default)
    {
        if (!_usuarioAtual.EstaAutenticado || _usuarioAtual.Id is null)
        {
            throw new AcessoNegadoException("Usuario nao autenticado.");
        }

        var usuario = await _usuarios.ObterPorIdAsync(_usuarioAtual.Id.Value, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Usuario", _usuarioAtual.Id.Value);

        if (!usuario.Autenticar(comando.SenhaAtual, _hash))
        {
            throw new AcessoNegadoException("A senha atual informada esta incorreta.");
        }

        // RNF0021: a mesma politica do cadastro vale para a troca de senha.
        if (!_politica.EhForte(comando.NovaSenha, out var motivo))
        {
            throw new ValidacaoException(nameof(comando.NovaSenha), motivo!);
        }

        usuario.DefinirSenhaHash(_hash.GerarHash(comando.NovaSenha));

        await _unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken);
    }
}
