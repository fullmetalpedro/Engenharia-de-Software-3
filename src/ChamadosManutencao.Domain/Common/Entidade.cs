namespace ChamadosManutencao.Domain.Common;

/// <summary>
/// Base de toda entidade do dominio. A identidade e o unico criterio de igualdade.
/// O identificador e recebido pronto: o dominio nunca gera Guid por conta propria
/// (secao 18 da especificacao), quem gera e a camada de aplicacao via IGeradorId.
/// </summary>
public abstract class Entidade
{
    protected Entidade(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ExcecaoDeDominio("O identificador da entidade nao pode ser vazio.");
        }

        Id = id;
    }

    /// <summary>Construtor usado apenas pelo EF Core na materializacao.</summary>
    protected Entidade()
    {
    }

    public Guid Id { get; private set; }

    public override bool Equals(object? obj) =>
        obj is Entidade outra && outra.GetType() == GetType() && outra.Id == Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
