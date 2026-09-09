using ChamadosManutencao.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace ChamadosManutencao.Infrastructure.Armazenamento;

/// <summary>
/// Grava os anexos em volume local sob storage/. Devolve sempre caminho relativo: o arquivo
/// nunca e servido por caminho direto, apenas por endpoint autenticado.
/// </summary>
public sealed class ArmazenamentoEmVolume : IArmazenamentoArquivos
{
    private readonly string _raiz;

    public ArmazenamentoEmVolume(IConfiguration configuracao)
    {
        var configurada = configuracao["Armazenamento:RaizDosAnexos"] ?? "storage";

        _raiz = Path.IsPathRooted(configurada)
            ? configurada
            : Path.Combine(AppContext.BaseDirectory, configurada);

        Directory.CreateDirectory(_raiz);
    }

    public async Task<string> GravarAsync(
        string caminhoRelativo,
        Stream conteudo,
        CancellationToken cancellationToken = default)
    {
        var destino = CaminhoAbsoluto(caminhoRelativo);

        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);

        await using var arquivo = File.Create(destino);
        await conteudo.CopyToAsync(arquivo, cancellationToken);

        return caminhoRelativo.Replace('\\', '/');
    }

    public Task<Stream> AbrirLeituraAsync(
        string caminhoRelativo,
        CancellationToken cancellationToken = default)
    {
        var origem = CaminhoAbsoluto(caminhoRelativo);

        if (!File.Exists(origem))
        {
            throw new FileNotFoundException("Arquivo nao encontrado no armazenamento.", caminhoRelativo);
        }

        Stream fluxo = File.OpenRead(origem);

        return Task.FromResult(fluxo);
    }

    public Task RemoverAsync(string caminhoRelativo, CancellationToken cancellationToken = default)
    {
        var alvo = CaminhoAbsoluto(caminhoRelativo);

        if (File.Exists(alvo))
        {
            File.Delete(alvo);
        }

        return Task.CompletedTask;
    }

    public bool Existe(string caminhoRelativo) => File.Exists(CaminhoAbsoluto(caminhoRelativo));

    /// <summary>
    /// Resolve o caminho dentro da raiz e recusa qualquer tentativa de sair dela
    /// (path traversal com ".." no nome do arquivo).
    /// </summary>
    private string CaminhoAbsoluto(string caminhoRelativo)
    {
        var combinado = Path.GetFullPath(Path.Combine(_raiz, caminhoRelativo));
        var raizNormalizada = Path.GetFullPath(_raiz);

        if (!combinado.StartsWith(raizNormalizada, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Caminho de arquivo fora da raiz de armazenamento.");
        }

        return combinado;
    }
}
