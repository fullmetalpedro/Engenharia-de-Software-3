using System.Globalization;
using System.Text;
using ChamadosManutencao.Application.Abstractions;
using ChamadosManutencao.Application.Common;
using ChamadosManutencao.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ChamadosManutencao.Application.UC09Analise;

/// <summary>
/// Parametros da analise de chamados (RF0071 a RF0073, RN0061, RN0062, RN0063).
/// </summary>
public sealed record FiltroDeAnalise(
    DateTimeOffset DataInicio,
    DateTimeOffset DataFim,
    IReadOnlyCollection<Guid>? CategoriaIds = null,
    IReadOnlyCollection<Guid>? TecnicoIds = null,
    string AgruparPor = "categoria");

public sealed record PeriodoDaAnaliseDto(string Inicio, string Fim);

public sealed record SerieDaAnaliseDto(string Id, string Legenda, IReadOnlyCollection<int> Valores);

/// <summary>
/// Resposta pronta para o grafico de linhas da RNF0051: eixo X com mes/ano, eixo Y com a
/// quantidade e uma serie por categoria ou tecnico selecionado.
/// </summary>
public sealed record AnaliseDeChamadosDto(
    PeriodoDaAnaliseDto Periodo,
    IReadOnlyCollection<string> EixoX,
    IReadOnlyCollection<SerieDaAnaliseDto> Series,
    int TotalChamados);

/// <summary>RN0062: intervalo de 1 a 24 meses.</summary>
public sealed class FiltroDeAnaliseValidator : AbstractValidator<FiltroDeAnalise>
{
    public const int IntervaloMinimoEmMeses = 1;
    public const int IntervaloMaximoEmMeses = 24;

    public FiltroDeAnaliseValidator()
    {
        RuleFor(f => f.DataInicio).NotEmpty();
        RuleFor(f => f.DataFim).NotEmpty();

        RuleFor(f => f.DataFim)
            .GreaterThanOrEqualTo(f => f.DataInicio)
            .WithMessage("A data de fim deve ser posterior a data de inicio.");

        RuleFor(f => f)
            .Must(EstaNoIntervaloPermitido)
            .WithMessage($"O intervalo da analise deve ser de no minimo {IntervaloMinimoEmMeses} "
                + $"e no maximo {IntervaloMaximoEmMeses} meses.")
            .WithName("periodo");

        RuleFor(f => f.AgruparPor)
            .Must(valor => valor is "categoria" or "tecnico")
            .WithMessage("O agrupamento deve ser 'categoria' ou 'tecnico'.");
    }

    private static bool EstaNoIntervaloPermitido(FiltroDeAnalise filtro)
    {
        if (filtro.DataFim < filtro.DataInicio)
        {
            return false;
        }

        var meses = QuantidadeDeMeses(filtro.DataInicio, filtro.DataFim);

        return meses is >= IntervaloMinimoEmMeses and <= IntervaloMaximoEmMeses;
    }

    /// <summary>RN0061: o agrupamento e por mes, entao o intervalo tambem e contado em meses.</summary>
    public static int QuantidadeDeMeses(DateTimeOffset inicio, DateTimeOffset fim) =>
        ((fim.Year - inicio.Year) * 12) + fim.Month - inicio.Month + 1;
}

/// <summary>
/// Analisa o historico de chamados por periodo, categoria e tecnico.
/// Requisitos: RF0071, RF0072, RF0073, RN0061, RN0062, RN0063, RNF0011, RNF0051.
/// Caso de uso: UC09.
/// </summary>
public sealed class AnalisarChamadosHandler
{
    private readonly IContextoDeLeitura _leitura;

    public AnalisarChamadosHandler(IContextoDeLeitura leitura) => _leitura = leitura;

    public async Task<AnaliseDeChamadosDto> ExecutarAsync(
        FiltroDeAnalise filtro,
        CancellationToken cancellationToken = default)
    {
        var inicio = new DateTimeOffset(
            filtro.DataInicio.Year,
            filtro.DataInicio.Month,
            1,
            0, 0, 0,
            TimeSpan.Zero);

        var fimExclusivo = new DateTimeOffset(
            filtro.DataFim.Year,
            filtro.DataFim.Month,
            1,
            0, 0, 0,
            TimeSpan.Zero).AddMonths(1);

        var consulta =
            from chamado in _leitura.Chamados
            join tipo in _leitura.TiposServico on chamado.TipoServicoId equals tipo.Id
            where chamado.DataHoraAbertura >= inicio
                && chamado.DataHoraAbertura < fimExclusivo
                // RN0063: chamados cancelados ficam fora da analise.
                && chamado.Status != StatusChamado.Cancelado
            select new
            {
                chamado.DataHoraAbertura,
                chamado.TecnicoId,
                tipo.CategoriaServicoId
            };

        if (filtro.CategoriaIds is { Count: > 0 })
        {
            var categorias = filtro.CategoriaIds.ToList();
            consulta = consulta.Where(c => categorias.Contains(c.CategoriaServicoId));
        }

        if (filtro.TecnicoIds is { Count: > 0 })
        {
            var tecnicos = filtro.TecnicoIds.ToList();
            consulta = consulta.Where(c => c.TecnicoId != null && tecnicos.Contains(c.TecnicoId.Value));
        }

        var agrupaPorTecnico = filtro.AgruparPor == "tecnico";

        // RN0061: agrupamento por mes de abertura.
        var linhas = await consulta
            .GroupBy(c => new
            {
                Ano = c.DataHoraAbertura.Year,
                Mes = c.DataHoraAbertura.Month,
                Chave = agrupaPorTecnico ? c.TecnicoId : c.CategoriaServicoId
            })
            .Select(grupo => new
            {
                grupo.Key.Ano,
                grupo.Key.Mes,
                grupo.Key.Chave,
                Quantidade = grupo.Count()
            })
            .ToListAsync(cancellationToken);

        var eixoX = MontarEixoX(inicio, fimExclusivo);

        var legendas = await MontarLegendasAsync(
            agrupaPorTecnico,
            linhas.Where(l => l.Chave is not null).Select(l => l.Chave!.Value).Distinct().ToList(),
            cancellationToken);

        var series = legendas
            .Select(legenda => new SerieDaAnaliseDto(
                legenda.Key.ToString(),
                legenda.Value,
                eixoX
                    .Select(rotulo => linhas
                        .Where(l => l.Chave == legenda.Key && Rotulo(l.Ano, l.Mes) == rotulo)
                        .Sum(l => l.Quantidade))
                    .ToList()))
            .OrderBy(serie => serie.Legenda)
            .ToList();

        return new AnaliseDeChamadosDto(
            new PeriodoDaAnaliseDto(eixoX.First(), eixoX.Last()),
            eixoX,
            series,
            linhas.Sum(l => l.Quantidade));
    }

    /// <summary>RF0074: exportacao dos dados apresentados no dashboard.</summary>
    public async Task<byte[]> ExportarCsvAsync(
        FiltroDeAnalise filtro,
        CancellationToken cancellationToken = default)
    {
        var analise = await ExecutarAsync(filtro, cancellationToken);

        var csv = new StringBuilder();
        csv.Append("serie");

        foreach (var mes in analise.EixoX)
        {
            csv.Append(';').Append(mes);
        }

        csv.Append(";total").AppendLine();

        foreach (var serie in analise.Series)
        {
            csv.Append(Escapar(serie.Legenda));

            foreach (var valor in serie.Valores)
            {
                csv.Append(';').Append(valor.ToString(CultureInfo.InvariantCulture));
            }

            csv.Append(';').Append(serie.Valores.Sum()).AppendLine();
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    private static string Escapar(string valor) =>
        valor.Contains(';') || valor.Contains('"')
            ? $"\"{valor.Replace("\"", "\"\"")}\""
            : valor;

    private static List<string> MontarEixoX(DateTimeOffset inicio, DateTimeOffset fimExclusivo)
    {
        var rotulos = new List<string>();

        for (var mes = inicio; mes < fimExclusivo; mes = mes.AddMonths(1))
        {
            rotulos.Add(Rotulo(mes.Year, mes.Month));
        }

        return rotulos;
    }

    private static string Rotulo(int ano, int mes) => $"{ano:0000}-{mes:00}";

    private async Task<Dictionary<Guid, string>> MontarLegendasAsync(
        bool agrupaPorTecnico,
        List<Guid> chaves,
        CancellationToken cancellationToken)
    {
        if (chaves.Count == 0)
        {
            return [];
        }

        return agrupaPorTecnico
            ? await _leitura.Tecnicos
                .Where(t => chaves.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.NomeCompleto, cancellationToken)
            : await _leitura.CategoriasServico
                .Where(c => chaves.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Nome, cancellationToken);
    }
}
