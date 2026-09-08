using FluentValidation;

namespace ChamadosManutencao.Application.Common;

/// <summary>
/// Executa o validator do comando antes do handler. Os endpoints chamam este metodo para nao
/// repetir o mesmo bloco de validacao em cada rota.
/// </summary>
public static class Validacao
{
    public static async Task GarantirValidoAsync<T>(
        IValidator<T>? validator,
        T comando,
        CancellationToken cancellationToken = default)
    {
        if (validator is null)
        {
            return;
        }

        var resultado = await validator.ValidateAsync(comando, cancellationToken);

        if (!resultado.IsValid)
        {
            throw new ValidacaoException(resultado.Errors
                .GroupBy(erro => erro.PropertyName)
                .ToDictionary(
                    grupo => grupo.Key,
                    grupo => grupo.Select(erro => erro.ErrorMessage).ToArray()));
        }
    }
}
