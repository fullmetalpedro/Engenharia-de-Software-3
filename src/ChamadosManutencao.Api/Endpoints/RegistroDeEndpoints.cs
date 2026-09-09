namespace ChamadosManutencao.Api.Endpoints;

/// <summary>
/// Registro unico dos endpoints, um arquivo por caso de uso, na ordem da matriz de
/// rastreabilidade.
/// </summary>
public static class RegistroDeEndpoints
{
    public static IEndpointRouteBuilder MapearEndpoints(this IEndpointRouteBuilder rotas)
    {
        rotas.MapearAutenticacao();
        rotas.MapearUC01Clientes();
        rotas.MapearUC02Tecnicos();
        rotas.MapearUC03Catalogo();
        rotas.MapearUC04Chamados();
        rotas.MapearUC05Triagem();
        rotas.MapearUC06Agendamento();
        rotas.MapearUC07Atendimento();
        rotas.MapearUC08Avaliacao();
        rotas.MapearUC09Analise();
        rotas.MapearUC10FormasDePagamento();
        rotas.MapearUC11Faturas();
        rotas.MapearUC12Garantia();

        return rotas;
    }
}
