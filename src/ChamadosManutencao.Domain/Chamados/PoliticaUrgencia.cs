using ChamadosManutencao.Domain.Catalogo;
using ChamadosManutencao.Domain.Enums;

namespace ChamadosManutencao.Domain.Chamados;

/// <summary>
/// Servico de dominio da RN0031: chamado de categoria de risco (eletrica, hidraulica) aberto
/// com indicacao de risco recebe urgencia ALTA automaticamente. Sem indicacao de risco, ou em
/// categoria que nao e de risco, vale a urgencia informada pelo cliente.
/// </summary>
public static class PoliticaUrgencia
{
    public static Urgencia Definir(
        CategoriaServico categoria,
        bool indicacaoDeRisco,
        Urgencia urgenciaInformada)
    {
        ArgumentNullException.ThrowIfNull(categoria);

        return categoria.CategoriaDeRisco && indicacaoDeRisco
            ? Urgencia.Alta
            : urgenciaInformada;
    }
}
