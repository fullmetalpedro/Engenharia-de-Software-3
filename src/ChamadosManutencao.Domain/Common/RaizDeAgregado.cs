namespace ChamadosManutencao.Domain.Common;

/// <summary>
/// Raiz de agregado. Acumula eventos de dominio que a unidade de trabalho despacha
/// depois que o SaveChanges conclui com sucesso.
/// </summary>
public abstract class RaizDeAgregado : Entidade
{
    private readonly List<IEventoDeDominio> _eventos = [];

    protected RaizDeAgregado(Guid id) : base(id)
    {
    }

    protected RaizDeAgregado()
    {
    }

    public IReadOnlyCollection<IEventoDeDominio> Eventos => _eventos.AsReadOnly();

    protected void RegistrarEvento(IEventoDeDominio evento) => _eventos.Add(evento);

    public void LimparEventos() => _eventos.Clear();
}
