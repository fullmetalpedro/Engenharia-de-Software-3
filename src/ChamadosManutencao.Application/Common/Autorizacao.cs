using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Domain.Enums;

namespace ChamadosManutencao.Application.Common;

/// <summary>
/// Checagens de propriedade que a politica de papel sozinha nao cobre: o cliente so enxerga o
/// proprio cadastro e os proprios chamados, o tecnico so age no chamado atribuido a ele.
/// </summary>
public static class Autorizacao
{
    public static Guid ExigirAutenticado(IUsuarioAtual usuario) =>
        usuario is { EstaAutenticado: true, Id: not null }
            ? usuario.Id.Value
            : throw new AcessoNegadoException("Usuario nao autenticado.");

    public static bool EhAdministrador(IUsuarioAtual usuario) =>
        usuario.Papel == PapelUsuario.Administrador;

    /// <summary>Administrador passa sempre; os demais so acessam os proprios dados.</summary>
    public static void ExigirDonoOuAdministrador(IUsuarioAtual usuario, Guid donoId)
    {
        var id = ExigirAutenticado(usuario);

        if (EhAdministrador(usuario) || id == donoId)
        {
            return;
        }

        throw new AcessoNegadoException("Este recurso pertence a outro usuario.");
    }

    /// <summary>Exige que o usuario autenticado seja exatamente o dono informado.</summary>
    public static void ExigirDono(IUsuarioAtual usuario, Guid donoId)
    {
        var id = ExigirAutenticado(usuario);

        if (id != donoId)
        {
            throw new AcessoNegadoException("Este recurso pertence a outro usuario.");
        }
    }
}
