using ChamadosManutencao.Domain.Chamados;
using ChamadosManutencao.Domain.Enums;
using ChamadosManutencao.Domain.Financeiro;

namespace ChamadosManutencao.Application.Abstractions;

/// <summary>
/// Fonte unica de tempo do sistema. Nenhum ponto do codigo chama DateTime.Now diretamente,
/// senao os testes de prazo (RN0035, RN0043, RN0052, RN0072) ficariam impossiveis.
/// </summary>
public interface IRelogio
{
    DateTimeOffset Agora { get; }
}

/// <summary>Usuario autenticado na requisicao atual, lido das claims do JWT.</summary>
public interface IUsuarioAtual
{
    Guid? Id { get; }

    PapelUsuario? Papel { get; }

    bool EstaAutenticado { get; }
}

/// <summary>
/// Unidade de trabalho. Confirma a transacao e so entao despacha os eventos de dominio
/// acumulados nas raizes de agregado.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Fonte dos numeros e codigos sequenciais do sistema (RNF0023, RNF0031, RNF0042).
/// As sequences vivem no PostgreSQL; nada de MAX(numero) + 1.
/// </summary>
public interface IGeradorDeSequencias
{
    Task<long> ProximoNumeroDeChamadoAsync(CancellationToken cancellationToken = default);

    Task<long> ProximoNumeroDeFaturaAsync(CancellationToken cancellationToken = default);

    Task<string> ProximoCodigoDeClienteAsync(CancellationToken cancellationToken = default);

    Task<string> ProximoCodigoDeTecnicoAsync(CancellationToken cancellationToken = default);
}

/// <summary>Politica de senha forte (RNF0021).</summary>
public interface IPoliticaDeSenha
{
    bool EhForte(string senha, out string? motivo);
}

/// <summary>
/// Gateway de pagamento. Implementacao simulada nesta versao: nao ha integracao real no escopo.
/// </summary>
public interface IGatewayPagamento
{
    /// <summary>
    /// RNF0061: devolve o token que substitui o numero do cartao. O numero completo nunca e
    /// persistido nem registrado em log.
    /// </summary>
    Task<TokenDeCartao> TokenizarCartaoAsync(
        DadosDeCartao dados,
        CancellationToken cancellationToken = default);

    Task<RetornoDeAutorizacao> AutorizarTransacaoAsync(
        string token,
        decimal valor,
        CancellationToken cancellationToken = default);
}

/// <summary>Dados completos do cartao, usados apenas em memoria durante a tokenizacao.</summary>
public sealed record DadosDeCartao(
    string NumeroCompleto,
    string NomeTitular,
    string Validade,
    string CodigoSeguranca);

/// <summary>Resultado da tokenizacao (RNF0061).</summary>
public sealed record TokenDeCartao(
    string Token,
    string Bandeira,
    string UltimosQuatroDigitos,
    string Validade);

/// <summary>Resultado da autorizacao de uma transacao (RF0084).</summary>
public sealed record RetornoDeAutorizacao(
    bool Aprovada,
    string IdentificadorTransacao,
    string CodigoDeRetorno,
    string Mensagem);

/// <summary>
/// Notificacao ao cliente (RNF0041). A implementacao padrao registra log estruturado e grava
/// em notificacao_enviada, o que permite trocar por SMTP ou push sem tocar no dominio.
/// </summary>
public interface INotificador
{
    Task NotificarMudancaDeStatusAsync(
        Chamado chamado,
        StatusChamado? statusAnterior,
        CancellationToken cancellationToken = default);

    Task SolicitarAvaliacaoAsync(Chamado chamado, CancellationToken cancellationToken = default);

    Task NotificarVencimentoDeFaturaAsync(Fatura fatura, CancellationToken cancellationToken = default);
}

/// <summary>
/// Armazenamento dos anexos em volume local. Hoje o sistema so grava: nenhum requisito pede
/// download, entao a porta expoe apenas a escrita.
/// </summary>
public interface IArmazenamentoArquivos
{
    Task<string> GravarAsync(
        string caminhoRelativo,
        Stream conteudo,
        CancellationToken cancellationToken = default);
}
