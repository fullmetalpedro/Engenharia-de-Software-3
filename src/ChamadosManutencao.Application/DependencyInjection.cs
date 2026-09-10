using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ChamadosManutencao.Application;

/// <summary>
/// Composicao da camada de aplicacao: um handler por comando ou consulta, um validator por
/// comando de entrada.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AdicionarAplicacao(this IServiceCollection servicos)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Tempo de vida por requisicao, para acompanhar o do DbContext.
        var handlers = assembly.GetTypes()
            .Where(tipo => tipo is { IsClass: true, IsAbstract: false }
                && tipo.Name.EndsWith("Handler", StringComparison.Ordinal));

        foreach (var handler in handlers)
        {
            servicos.AddScoped(handler);
        }

        servicos.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped);

        return servicos;
    }
}
