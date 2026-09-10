using ChamadosManutencao.Domain.Catalogo;
using ChamadosManutencao.Domain.Enums;

namespace ChamadosManutencao.Domain.Chamados;

/// <summary>
/// Servico de dominio da RN0031: chamado de categoria de risco (eletrica, hidraulica) aberto
/// com indicacao de risco recebe urgencia ALTA automaticamente.
///
/// Fora desse caso o chamado nasce com urgencia MEDIA e so o administrador a altera (RF0046).
/// O RF0041 lista os quatro dados que o cliente informa na abertura, e urgencia nao esta
/// entre eles.
/// </summary>
public static class PoliticaUrgencia
{
    /// <summary>Urgencia com que o chamado nasce, quando ninguem a classificou ainda.</summary>
    public const Urgencia Padrao = Urgencia.Media;

    public static Urgencia Definir(CategoriaServico categoria, bool indicacaoDeRisco)
    {
        ArgumentNullException.ThrowIfNull(categoria);

        return categoria.CategoriaDeRisco && indicacaoDeRisco ? Urgencia.Alta : Padrao;
    }
}
