namespace ChamadosManutencao.Domain.Enums;

/// <summary>Tipo do imovel do cliente (RN0012).</summary>
public enum TipoImovel
{
    Casa = 1,
    Apartamento = 2,
    Comercio = 3
}

/// <summary>Urgencia do chamado (RF0046, RN0031).</summary>
public enum Urgencia
{
    Baixa = 1,
    Media = 2,
    Alta = 3
}

/// <summary>Status do chamado (RN0034).</summary>
public enum StatusChamado
{
    Aberto = 1,
    EmAnalise = 2,
    Agendado = 3,
    EmAtendimento = 4,
    Concluido = 5,
    Cancelado = 6
}

/// <summary>Status do agendamento (RF0051, RF0052, RF0053).</summary>
public enum StatusAgendamento
{
    Proposto = 1,
    Confirmado = 2,
    Reagendado = 3,
    Cancelado = 4
}

/// <summary>Status do orcamento (RF0055, RF0056, RN0042, RN0043).</summary>
public enum StatusOrcamento
{
    Pendente = 1,
    Aprovado = 2,
    Recusado = 3,
    Expirado = 4
}

/// <summary>Natureza do item do orcamento (RN0071).</summary>
public enum TipoItem
{
    Peca = 1,
    MaoDeObra = 2
}

/// <summary>Status da fatura (RF0082, RF0083, RF0084).</summary>
public enum StatusFatura
{
    Emitida = 1,
    Paga = 2,
    Vencida = 3,
    Cancelada = 4
}

/// <summary>Status do pagamento (RF0084).</summary>
public enum StatusPagamento
{
    Processando = 1,
    Aprovado = 2,
    Recusado = 3,
    Estornado = 4
}

/// <summary>Quem propos o agendamento (RF0051, RF0053).</summary>
public enum OrigemProposta
{
    Tecnico = 1,
    Administrador = 2,
    Cliente = 3
}

/// <summary>Origem do anexo, usada para aplicar RNF0043 por grupo (ver D09).</summary>
public enum OrigemAnexo
{
    ChamadoAbertura = 1,
    ConclusaoAtendimento = 2,
    DocumentoTecnico = 3
}

/// <summary>Papel do usuario no sistema, usado nas politicas de autorizacao.</summary>
public enum PapelUsuario
{
    Cliente = 1,
    Tecnico = 2,
    Administrador = 3
}

/// <summary>Tipo da chave PIX cadastrada como forma de pagamento (RF0081).</summary>
public enum TipoChavePix
{
    Cpf = 1,
    Email = 2,
    Telefone = 3,
    Aleatoria = 4
}
