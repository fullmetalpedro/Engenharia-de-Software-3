namespace ChamadosManutencao.Application.Common;

/// <summary>Recurso inexistente. A API traduz para 404.</summary>
public sealed class RecursoNaoEncontradoException : Exception
{
    public RecursoNaoEncontradoException(string recurso, object chave)
        : base($"{recurso} nao encontrado(a): {chave}.")
    {
        Recurso = recurso;
        Chave = chave;
    }

    public string Recurso { get; }

    public object Chave { get; }
}

/// <summary>Operacao negada ao usuario autenticado. A API traduz para 403.</summary>
public sealed class AcessoNegadoException : Exception
{
    public AcessoNegadoException(string mensagem) : base(mensagem)
    {
    }
}

/// <summary>Conflito com o estado atual do recurso. A API traduz para 409.</summary>
public sealed class ConflitoException : Exception
{
    public ConflitoException(string mensagem, string? requisito = null) : base(mensagem) =>
        Requisito = requisito;

    public string? Requisito { get; }
}

/// <summary>Falha de validacao de entrada. A API traduz para 400 com a colecao errors.</summary>
public sealed class ValidacaoException : Exception
{
    public ValidacaoException(IDictionary<string, string[]> erros)
        : base("Um ou mais campos estao invalidos.") =>
        Erros = erros;

    public ValidacaoException(string campo, string mensagem)
        : this(new Dictionary<string, string[]> { [campo] = [mensagem] })
    {
    }

    public IDictionary<string, string[]> Erros { get; }
}

/// <summary>
/// Credencial invalida ou usuario inativo no login. A API traduz para 401: o pedido nao foi
/// autenticado, diferente do 403 que pressupoe usuario conhecido sem permissao.
/// </summary>
public sealed class CredenciaisInvalidasException : Exception
{
    public CredenciaisInvalidasException(string mensagem) : base(mensagem)
    {
    }
}
