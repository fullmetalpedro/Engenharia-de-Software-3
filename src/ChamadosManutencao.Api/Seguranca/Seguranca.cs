using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Pessoas;
using Microsoft.IdentityModel.Tokens;

namespace ChamadosManutencao.Api.Seguranca;

/// <summary>Nomes das politicas de autorizacao usadas pelos endpoints.</summary>
public static class Politicas
{
    public const string Cliente = nameof(PapelUsuario.Cliente);
    public const string Tecnico = nameof(PapelUsuario.Tecnico);
    public const string Administrador = nameof(PapelUsuario.Administrador);
    public const string ClienteOuAdministrador = "ClienteOuAdministrador";
    public const string TecnicoOuAdministrador = "TecnicoOuAdministrador";

    /// <summary>RF0053: o reagendamento e pedido pelo cliente ou pelo tecnico.</summary>
    public const string ClienteOuTecnico = "ClienteOuTecnico";
}

/// <summary>Configuracao do JWT lida de appsettings/ambiente.</summary>
public sealed class OpcoesDeJwt
{
    public const string Secao = "Jwt";

    public string Emissor { get; set; } = "ChamadosManutencao";

    public string Audiencia { get; set; } = "ChamadosManutencao.Api";

    public string ChaveSecreta { get; set; } = string.Empty;

    public int MinutosDeValidade { get; set; } = 60;
}

/// <summary>
/// Emissao do token JWT. As claims carregam o identificador e o papel, lidos depois por
/// <see cref="UsuarioAtual"/>.
/// </summary>
public sealed class GeradorDeTokenJwt : IGeradorDeToken
{
    private readonly OpcoesDeJwt _opcoes;
    private readonly IRelogio _relogio;

    public GeradorDeTokenJwt(OpcoesDeJwt opcoes, IRelogio relogio)
    {
        _opcoes = opcoes;
        _relogio = relogio;
    }

    public TokenEmitido Emitir(Usuario usuario)
    {
        var expiraEm = _relogio.Agora.AddMinutes(_opcoes.MinutosDeValidade);
        var papel = usuario.Papel.ToString();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email),
            new(JwtRegisteredClaimNames.Name, usuario.NomeCompleto),
            new(ClaimTypes.Role, papel),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString())
        };

        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opcoes.ChaveSecreta));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opcoes.Emissor,
            audience: _opcoes.Audiencia,
            claims: claims,
            notBefore: _relogio.Agora.UtcDateTime,
            expires: expiraEm.UtcDateTime,
            signingCredentials: credenciais);

        return new TokenEmitido(new JwtSecurityTokenHandler().WriteToken(token), expiraEm, papel);
    }
}

/// <summary>Usuario autenticado da requisicao atual, lido das claims do JWT.</summary>
public sealed class UsuarioAtual : IUsuarioAtual
{
    private readonly IHttpContextAccessor _acessor;

    public UsuarioAtual(IHttpContextAccessor acessor) => _acessor = acessor;

    public Guid? Id
    {
        get
        {
            var valor = _acessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? _acessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(valor, out var id) ? id : null;
        }
    }

    public PapelUsuario? Papel
    {
        get
        {
            var valor = _acessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role);

            return Enum.TryParse<PapelUsuario>(valor, out var papel) ? papel : null;
        }
    }

    public bool EstaAutenticado => _acessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
