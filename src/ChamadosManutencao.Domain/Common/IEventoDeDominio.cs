namespace ChamadosManutencao.Domain.Common;

/// <summary>
/// Marcador de evento de dominio. Todo evento carrega o momento em que ocorreu,
/// fornecido por quem o cria (o dominio nao le o relogio do sistema).
/// </summary>
public interface IEventoDeDominio
{
    DateTimeOffset OcorridoEm { get; }
}
