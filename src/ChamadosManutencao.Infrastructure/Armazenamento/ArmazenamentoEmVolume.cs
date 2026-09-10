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

    /// <summary>
    /// Resolve o caminho dentro da raiz e recusa qualquer tentativa de sair dela
    /// (path traversal com ".." no nome do arquivo).
    /// </summary>
    private string CaminhoAbsoluto(string caminhoRelativo)
    {
        var combinado = Path.GetFullPath(Path.Combine(_raiz, caminhoRelativo));

        // O separador no fim evita que uma pasta irma com o mesmo prefixo (storage-backup)
        // passe pela verificacao. A comparacao acompanha o sistema de arquivos, que no
        // Windows ignora caixa.
        var raizNormalizada = Path.GetFullPath(_raiz).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!combinado.StartsWith(raizNormalizada, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                "Caminho de arquivo fora da raiz de armazenamento.");
        }

        return combinado;
    }
}
