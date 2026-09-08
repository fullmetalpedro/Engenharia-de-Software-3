namespace ChamadosManutencao.Domain.Common;

/// <summary>
/// Guardas de invariante usadas nos construtores das entidades. Codigo explicito,
/// mensagem em portugues, uma excecao por violacao.
/// </summary>
public static class Garantir
{
    public static string TextoPreenchido(string? valor, string nomeDoCampo, string? requisito = null)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ExcecaoDeDominio($"O campo {nomeDoCampo} e obrigatorio.", requisito);
        }

        return valor.Trim();
    }

    public static string TextoComTamanhoMaximo(
        string? valor,
        int tamanhoMaximo,
        string nomeDoCampo,
        string? requisito = null)
    {
        var texto = TextoPreenchido(valor, nomeDoCampo, requisito);

        if (texto.Length > tamanhoMaximo)
        {
            throw new ExcecaoDeDominio(
                $"O campo {nomeDoCampo} deve ter no maximo {tamanhoMaximo} caracteres.",
                requisito);
        }

        return texto;
    }

    public static string Cpf(string? valor, string? requisito = null)
    {
        var somenteDigitos = new string((valor ?? string.Empty).Where(char.IsDigit).ToArray());

        if (somenteDigitos.Length != 11)
        {
            throw new ExcecaoDeDominio("O CPF deve conter 11 digitos.", requisito);
        }

        return somenteDigitos;
    }

    public static string Email(string? valor, string? requisito = null)
    {
        var email = TextoPreenchido(valor, "e-mail", requisito);

        var arroba = email.IndexOf('@');
        var ponto = email.LastIndexOf('.');

        if (arroba <= 0 || ponto < arroba + 2 || ponto == email.Length - 1)
        {
            throw new ExcecaoDeDominio($"O e-mail '{email}' nao e valido.", requisito);
        }

        return email.ToLowerInvariant();
    }

    public static string Cep(string? valor, string? requisito = null)
    {
        var somenteDigitos = new string((valor ?? string.Empty).Where(char.IsDigit).ToArray());

        if (somenteDigitos.Length != 8)
        {
            throw new ExcecaoDeDominio("O CEP deve conter 8 digitos.", requisito);
        }

        return somenteDigitos;
    }

    public static decimal ValorNaoNegativo(decimal valor, string nomeDoCampo, string? requisito = null)
    {
        if (valor < 0)
        {
            throw new ExcecaoDeDominio($"O campo {nomeDoCampo} nao pode ser negativo.", requisito);
        }

        return valor;
    }

    public static void NaoNulo<T>(T? valor, string nomeDoCampo, string? requisito = null)
        where T : class
    {
        if (valor is null)
        {
            throw new ExcecaoDeDominio($"O campo {nomeDoCampo} e obrigatorio.", requisito);
        }
    }
}
