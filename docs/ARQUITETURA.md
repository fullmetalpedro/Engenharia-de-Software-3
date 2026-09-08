# Arquitetura — Sistema de Abertura de Chamados de Manutenção

> **Fase 0 (Alinhamento).** Este documento é o entregável da Fase 0 do plano de execução.
> Nenhuma linha de código de produção foi escrita ainda. Ele registra o desenho proposto e,
> na seção 10, a lista de dúvidas, ambiguidades e contradições encontradas entre a
> especificação executável e os artefatos de análise. As pendências marcadas como
> **BLOQUEANTE** precisam de decisão antes da fase indicada.

| Item | Valor |
|---|---|
| Versão do documento | 0.1 |
| Data | 08/09/2026 |
| Fontes | DRS v0.1 (`DRS_Chamados_Manutencao.docx`), Diagramas v0.2 (`Diagramas_Chamados_Manutencao.pdf`), Matriz de rastreabilidade (`Matriz_Rastreabilidade_Chamados_Manutencao.xlsx`), Especificação executável (`PROMPT_Projeto_CSharp_Chamados_Manutencao.md`) |
| Total de requisitos | 78 (46 RF, 20 RN, 12 RNF) |

---

## 1. Conferência dos artefatos de análise

A leitura dos quatro artefatos confirma a contagem de 78 requisitos, mas eles **não estão todos
no mesmo documento**:

| Artefato | Versão | Conteúdo |
|---|---|---|
| `DRS_Chamados_Manutencao.docx` | 0.1 (02/09/2026) | 41 RF, 18 RN, 11 RNF = **70 requisitos** |
| `Diagramas_Chamados_Manutencao.pdf` | 0.2 (02/09/2026) | Os **8 requisitos adicionais**: RF0081–RF0085, RN0071, RN0072, RNF0061, mais o diagrama de casos de uso desses requisitos e o diagrama de classes consolidado |
| `Matriz_Rastreabilidade...xlsx` | — | 12 casos de uso (UC01–UC12) e a matriz completa dos 78 requisitos, com nota explicando que os 8 últimos vêm da versão 0.2 do DRS |

A especificação executável cita "Documento de Requisitos (DRS) com 78 requisitos". Na prática,
a fonte da verdade é o **conjunto** DRS v0.1 + PDF v0.2, consolidado pela matriz. Não é
contradição de conteúdo, é dispersão de fonte. Ver dúvida **D01**.

Conferência da contagem por tipo, somando as duas versões:

- **RF (46):** RF0011–RF0016 (6), RF0021–RF0027 (7), RF0031–RF0034 (4), RF0041–RF0050 (10), RF0051–RF0057 (7), RF0061–RF0063 (3), RF0071–RF0074 (4), RF0081–RF0085 (5).
- **RN (20):** RN0011, RN0012, RN0021–RN0023, RN0031–RN0035, RN0041–RN0043, RN0051, RN0052, RN0061–RN0063, RN0071, RN0072.
- **RNF (12):** RNF0011, RNF0012, RNF0021–RNF0023, RNF0031, RNF0032, RNF0041–RNF0043, RNF0051, RNF0061.

---

## 2. Visão geral da solução

Backend HTTP em .NET 10, sem interface gráfica, exposto como API REST versionada em
`/api/v1`, documentada por OpenAPI e renderizada por Scalar. Banco PostgreSQL 17 em container,
schema versionado por migrations do EF Core. Autenticação JWT Bearer com três papéis
(`Cliente`, `Tecnico`, `Administrador`).

```
┌──────────────────────────────────────────────────────────────────┐
│  Cliente HTTP (Scalar, curl, testes de integração)               │
└───────────────────────────┬──────────────────────────────────────┘
                            │ HTTP/JSON + JWT
┌───────────────────────────▼──────────────────────────────────────┐
│  ChamadosManutencao.Api                                          │
│  Minimal APIs agrupadas por caso de uso (UC01..UC12 + Auth)      │
│  Middlewares: ProblemDetails (RFC 9457), tempo de resposta       │
│  Segurança: emissão de JWT, políticas por papel                  │
└───────────────────────────┬──────────────────────────────────────┘
                            │ comandos e consultas
┌───────────────────────────▼──────────────────────────────────────┐
│  ChamadosManutencao.Application                                  │
│  Handlers por caso de uso, validators (FluentValidation),        │
│  DTOs, mapeamento manual, abstrações de infraestrutura           │
└───────────────────────────┬──────────────────────────────────────┘
                            │ entidades e serviços de domínio
┌───────────────────────────▼──────────────────────────────────────┐
│  ChamadosManutencao.Domain   (zero dependências externas)        │
│  Agregados, invariantes, máquina de estados, eventos de domínio  │
└──────────────────────────────────────────────────────────────────┘
                            ▲
┌───────────────────────────┴──────────────────────────────────────┐
│  ChamadosManutencao.Infrastructure                               │
│  EF Core + Npgsql, configurations, migrations, repositórios,     │
│  interceptor de auditoria, armazenamento de anexos, gateway de   │
│  pagamento simulado, notificador, background services            │
└───────────────────────────┬──────────────────────────────────────┘
                            │ Npgsql
┌───────────────────────────▼──────────────────────────────────────┐
│  PostgreSQL 17 (container Docker, volume chamados_pgdata)        │
└──────────────────────────────────────────────────────────────────┘
```

Direção de dependências: `Api → Application → Domain` e `Infrastructure → Application →
Domain`. O projeto `Api` referencia `Infrastructure` apenas no arquivo de composição de
serviços (`DependencyInjection`), para poder registrar as implementações concretas das
abstrações declaradas na `Application`.

---

## 3. Estrutura de projetos

Estrutura conforme a seção 4 da especificação, sem desvios:

```
ChamadosManutencao/
├── docker-compose.yml
├── .env.example
├── ChamadosManutencao.sln
├── Directory.Build.props          (net10.0, Nullable, TreatWarningsAsErrors)
├── docs/
│   ├── ARQUITETURA.md             (este documento)
│   ├── RASTREABILIDADE.md         (Fase 9, alimentado desde a Fase 5)
│   └── DECISOES.md                (registro de decisões, alimentado por fase)
├── src/
│   ├── ChamadosManutencao.Domain/
│   ├── ChamadosManutencao.Application/
│   ├── ChamadosManutencao.Infrastructure/
│   └── ChamadosManutencao.Api/
└── tests/
    ├── ChamadosManutencao.UnitTests/
    └── ChamadosManutencao.IntegrationTests/
```

**Localização:** o projeto vive em um repositório git próprio (`C:\Users\pedro\ChamadosManutencao`),
separado do repositório `eletiva-web` (exercícios PHP), para não misturar históricos e
`.gitignore`.

---

## 4. Modelo de domínio proposto

### 4.1 Agregados e suas fronteiras

O diagrama de classes consolidado é plano (todas as classes no mesmo nível). Para a
implementação, ele é organizado em agregados, com uma raiz responsável pelas invariantes:

| Agregado (raiz) | Entidades internas | Referências por Id |
|---|---|---|
| `Cliente` (`Usuario`) | `Imovel`, `FormaPagamento` (+ subclasses) | — |
| `Tecnico` (`Usuario`) | `AreaAtendimento`, documentos (`Anexo`) | `CategoriaServico` (especialidades) |
| `Administrador` (`Usuario`) | — | — |
| `CategoriaServico` | `TipoServico` | — |
| `Chamado` | `Anexo`, `HistoricoStatus`, `Agendamento` | `Cliente`, `Imovel`, `TipoServico`, `Tecnico`, `Chamado` (original), `Garantia` |
| `Atendimento` | `Orcamento` → `ItemOrcamento`, `Garantia` | `Chamado`, `Tecnico` |
| `Avaliacao` | `RespostaAvaliacao` | `Chamado`, `Administrador` |
| `Fatura` | `Pagamento` | `Atendimento`, `FormaPagamento` |

Referências entre agregados são feitas por identificador (`Guid`), nunca por navegação
direta, para manter as transações pequenas e as fronteiras explícitas.

### 4.2 Identidade e tipos

- Identificadores técnicos: `Guid` gerado com `Guid.CreateVersion7()` (v7, ordenável no tempo,
  disponível a partir do .NET 9), atrás da abstração `IGeradorId` para permitir teste
  determinístico. O diagrama usa `Integer`; a especificação executável manda `Guid` e
  **a especificação vence** (ver **D02**).
- Identificadores de negócio: `Chamado.Numero` e `Fatura.Numero` são `long`, vindos das
  sequences `seq_numero_chamado` e `seq_numero_fatura` (RNF0042).
- Códigos legíveis: `Cliente.CodigoCliente` (`CLI-000001`) e `Tecnico.CodigoTecnico`
  (`TEC-000001`), gerados a partir de sequences próprias (RNF0023, RNF0031).
- Datas: `DateTimeOffset` mapeado para `timestamptz`. Nenhum ponto do código chama
  `DateTime.Now`; tudo passa por `IRelogio.Agora`.
- Dinheiro: `decimal(12,2)`.

### 4.3 Máquina de estados do chamado (RN0034)

Tabela de transições que será implementada em `Chamado.AlterarStatus`. As transições marcadas
com `*` **não constam** do texto literal da RN0034 e vêm de outras regras — ver contradição
**D05**, que precisa de decisão antes da Fase 2.

```
ABERTO          → EM_ANALISE | CANCELADO
EM_ANALISE      → AGENDADO | CANCELADO
AGENDADO        → EM_ATENDIMENTO | AGENDADO (reagendamento *) | CANCELADO
EM_ATENDIMENTO  → CONCLUIDO | CANCELADO (*)   ← exigido por RN0042 e RN0043
CONCLUIDO       → EM_ANALISE (*)              ← exigido por RN0035 (reabertura, até 7 dias)
CANCELADO       → (estado final)
```

Qualquer outra transição lança `TransicaoDeStatusInvalidaException`. Toda transição bem
sucedida grava `HistoricoStatus` (RF0050) e publica o evento de domínio `StatusDoChamadoAlterado`,
consumido pelo notificador (RNF0041).

### 4.4 Eventos de domínio

Eventos são acumulados na raiz do agregado e despachados após `SaveChanges` bem-sucedido,
dentro da mesma unidade de trabalho:

| Evento | Origem | Consumidor | Requisito |
|---|---|---|---|
| `StatusDoChamadoAlterado` | `Chamado.AlterarStatus` | `INotificador` | RNF0041 |
| `AtendimentoConcluido` | `Atendimento.Concluir` | criação de `Garantia`, geração de `Fatura`, solicitação de avaliação | RN0051, RN0071, RF0082 |
| `FaturaVencida` | `VencimentoFaturaJob` | `INotificador` | RNF0041 (por analogia) |

### 4.5 Regras de negócio e seus donos

Segue a tabela da seção 8 da especificação, sem realocação. Cada RN terá pelo menos um teste
unitário com `[Trait("Requisito", "RNxxxx")]`.

---

## 5. Persistência

- Um `IEntityTypeConfiguration<T>` por entidade. Nenhuma data annotation nas entidades de
  domínio, nenhum mapeamento por convenção implícita.
- `UseSnakeCaseNamingConvention()` via `EFCore.NamingConventions`.
- Herança TPH em `Usuario` (discriminador `tipo_usuario`) e em `FormaPagamento`
  (discriminador `tipo_forma_pagamento`).
- Enums persistidos como `int` com conversor explícito e `CHECK` constraint por coluna.
- Concorrência otimista via `UseXminAsConcurrencyToken()` nas raízes `Chamado`, `Atendimento`,
  `Orcamento` e `Fatura`.
- `Cascade` nas composições (Cliente/Imovel, Chamado/Anexo, Chamado/HistoricoStatus,
  Orcamento/ItemOrcamento, Fatura/Pagamento); `Restrict` no resto.
- Índices conforme seção 6 da especificação. Como `Usuario` é TPH, os índices únicos de
  `cliente(cpf)` e `tecnico(cpf)` viram índices filtrados sobre a tabela única `usuario`
  (`WHERE tipo_usuario = 'Cliente'`), preservando a semântica pedida — ver **D03**.

### 5.1 Tabelas de infraestrutura (fora do modelo de domínio)

Três tabelas não aparecem no diagrama de classes porque são exigência de RNF, não de domínio:

| Tabela | Origem | Conteúdo |
|---|---|---|
| `log_transacao` | RNF0012 | data/hora, usuário, entidade, chave, operação, JSON anterior e novo |
| `notificacao_enviada` | RNF0041 | destinatário, canal, assunto, corpo, data/hora, referência |
| `__EFMigrationsHistory` | EF Core | controle de migrations |

---

## 6. Transversais

| Preocupação | Solução | Requisito |
|---|---|---|
| Erros | Middleware que traduz exceções de domínio e de validação para `ProblemDetails` (RFC 9457), com coleção `errors` nos erros de validação | seção 10 |
| Tempo de resposta | Middleware que mede a duração e emite log `Warning` acima de 1000 ms | RNF0011 |
| Paginação | `page`, `pageSize` (máximo 100), `AsNoTracking`, projeção direta para DTO | RNF0011 |
| Auditoria | Interceptor de `SaveChanges` gravando em `log_transacao` | RNF0012 |
| Senha | `IPoliticaDeSenha` (mínimo 8, maiúscula, minúscula, especial) + BCrypt work factor 12; `SenhaHash` nunca em DTO nem em log | RNF0021, RNF0022 |
| Logging | Serilog, console estruturado em Development, JSON fora dele; enriquecedor com `Id` do usuário atual | — |
| Autorização | Políticas nomeadas por papel, mais handlers de propriedade (`ClienteDono`, `TecnicoAtribuido`) | seção 10 |

---

## 7. Serviços simulados

Interfaces na `Application`, implementações na `Infrastructure`, registradas por configuração:

- `IGatewayPagamento` — `TokenizarCartao`, `AutorizarTransacao`. Simulação recusa valores
  terminados em `.99`. O número completo do cartão nunca é persistido nem logado (RNF0061).
- `INotificador` — grava log estruturado e persiste em `notificacao_enviada`.
- `IArmazenamentoArquivos` — grava em `storage/chamados/{chamadoId}/`, devolve caminho
  relativo; download somente por endpoint autenticado.
- `IRelogio` — `Agora` como `DateTimeOffset`; substituível nos testes de prazo.
- `IUsuarioAtual` — `Id`, `Papel`, `EstaAutenticado`, lidos das claims do JWT.
- `IGeradorId` — `NovoId()` com `Guid.CreateVersion7()`. **Adição proposta**, não listada na
  especificação, necessária porque a seção 18 proíbe `Guid.NewGuid()` espalhado pelo domínio.

---

## 8. Jobs em segundo plano

Quatro `BackgroundService` com intervalo de 15 minutos, idempotentes, com log do que
processaram: `ExpiracaoOrcamentoJob` (RN0043), `VencimentoFaturaJob`,
`EncerramentoJanelaAvaliacaoJob` (RN0052) e `EncerramentoGarantiaJob` (RN0072).

Os jobs usam `IRelogio`, nunca o relógio do sistema, e rodam em escopo próprio de DI.

---

## 9. Estratégia de testes

| Camada | Ferramenta | Escopo |
|---|---|---|
| Unitário | xUnit + Shouldly | Uma classe por RN, caminho feliz e ao menos uma violação; máquina de estados com teste parametrizado sobre todas as transições válidas e amostra de inválidas |
| Integração | xUnit + `WebApplicationFactory` + `Testcontainers.PostgreSql` | Ao menos um teste por endpoint da seção 10, banco real com migrations aplicadas, banco limpo entre classes |
| Jornada | Integração | Cadastro → chamado → triagem → agendamento → atendimento → orçamento → conclusão → fatura → pagamento → avaliação → garantia |
| Autorização | Integração | `401` sem token e `403` com papel errado, por endpoint restrito |

Cobertura mínima de `Domain`: 85%. Isso exige pacotes de infraestrutura de teste não listados
na seção 3 — ver **D04**.

---

## 10. Dúvidas, ambiguidades e contradições

Nenhum item abaixo foi resolvido unilateralmente. Onde há proposta, ela é sugestão a
confirmar; onde há **BLOQUEANTE**, a fase indicada não começa sem decisão.

### 10.1 Sobre os artefatos

**D01 — O DRS de 78 requisitos não existe como documento único.**
O `.docx` é a versão 0.1 (70 requisitos) e os 8 requisitos restantes só aparecem no PDF de
diagramas v0.2 e na matriz. *Proposta:* tratar DRS v0.1 + PDF v0.2 como fonte conjunta e
registrar isso em `docs/RASTREABILIDADE.md`. Confirmar se existe um `.docx` v0.2 que não foi
enviado.

**D02 — Tipo dos identificadores: diagrama diz `Integer`, especificação diz `Guid`.**
O diagrama de classes declara `id : Integer` em todas as entidades e `senha : String` em
`Usuario`. A especificação executável exige `Guid` (v7) e `SenhaHash`. *Resolução aplicada:*
a especificação vence, conforme o cabeçalho do próprio prompt. Registrado aqui por ser
divergência formal com um artefato aprovado.

**D03 — Nomes divergentes entre diagrama e especificação.**
`Tecnico.atendeRegiao(cep)` no diagrama contra `AtendeCep(cep)` na especificação;
`Agendamento.origemProposta : String` no diagrama contra enum na especificação;
`Pagamento.processar()` e `Atendimento.iniciar()` existem no diagrama e não na especificação.
*Resolução aplicada:* prevalece a especificação; métodos do diagrama ausentes na
especificação são implementados com o nome do diagrama quando não conflitam.

**D04 — Pacotes de teste fora da lista da seção 3.**
Para rodar e medir os testes são necessários `Microsoft.NET.Test.Sdk`,
`xunit.runner.visualstudio` e `coverlet.collector` (cobertura de 85% do `Domain`), além de
`Microsoft.AspNetCore.Mvc.Testing` para o `WebApplicationFactory`. A seção 18 pede
justificativa antes de adicionar pacote fora da lista. *Pedido de aprovação:* são pacotes de
infraestrutura de teste, sem efeito no código de produção.

### 10.2 Contradições entre regras (BLOQUEANTES para a Fase 2)

**D05 — A máquina de estados da RN0034 não comporta RN0035, RN0042 e RN0043. BLOQUEANTE.**
A RN0034 permite `CANCELADO` apenas a partir de `ABERTO`, `EM ANÁLISE` ou `AGENDADO`. Mas:
- RN0042 manda cancelar o chamado quando o cliente recusa o orçamento, e o orçamento só existe
  depois de `EM_ATENDIMENTO` (o técnico registra o orçamento durante o atendimento);
- RN0043 manda cancelar automaticamente na expiração das 48 horas, no mesmo estado;
- RN0035 manda voltar de `CONCLUIDO` para `EM ANÁLISE` na reabertura.

Ou seja, três regras exigem transições que a RN0034 não lista. *Proposta:* ampliar a tabela de
transições com `EM_ATENDIMENTO → CANCELADO` (justificada por RN0042/RN0043) e
`CONCLUIDO → EM_ANALISE` (justificada por RN0035), documentando a ampliação na
rastreabilidade. Confirmar antes de codificar.

**D06 — RN0033 contra RN0034 no cancelamento pelo cliente.**
RN0033 diz que o cliente cancela com status `ABERTO` ou `AGENDADO`; RN0034 admite cancelamento
também a partir de `EM ANÁLISE`. *Proposta:* `EM ANÁLISE` é cancelável, mas somente pelo
administrador (`POST /chamados/{id}/cancelamento-administrativo`), o que reconcilia as duas
regras. Confirmar.

**D07 — RN0032 (foto obrigatória na abertura) contra o desenho dos endpoints. BLOQUEANTE.**
A RN0032 bloqueia a *abertura* do chamado sem ao menos uma foto, mas anexos entram por um
endpoint separado (`POST /chamados/{id}/anexos`), que exige um chamado já criado. Como está,
é impossível abrir um chamado de categoria que exige foto. *Opções:*
(a) `POST /chamados` aceita `multipart/form-data` com os anexos junto;
(b) upload prévio devolvendo identificadores temporários, referenciados no corpo da abertura;
(c) o chamado nasce em estado de rascunho e só é validado ao enviar.
*Proposta:* opção (a), que é a mais simples e mantém a transação única.

**D08 — RN0041 exige detectar conflito de horário, mas o modelo não tem duração. BLOQUEANTE
para a Fase 6.**
`Agendamento` tem apenas `DataHoraProposta`. Sem duração (ou hora de fim) não existe
"horário já ocupado" a verificar, e o índice de exclusão sugerido na seção 8 precisaria de um
intervalo. *Proposta:* adotar uma janela fixa de atendimento configurável (padrão: 2 horas) e
tratar como conflito qualquer proposta que caia dentro da janela de outro agendamento
confirmado ou proposto do mesmo técnico. Confirmar a duração padrão.

**D09 — RNF0043 (máximo 5 anexos por chamado) contra as fotos de conclusão do RF0057.**
O diagrama tem duas associações distintas: "ilustrado por 1 → 0..5" (anexos do chamado) e
"fotos da conclusão 1 → 0..\*" (sem limite, ligadas ao atendimento). A especificação aplica
RNF0043 tanto na abertura quanto na conclusão. *Proposta:* o limite de 5 vale por origem
(`ChamadoAbertura`), e as fotos de conclusão são contadas separadamente, com o mesmo limite de
10 MB por arquivo. Confirmar.

### 10.3 Lacunas de especificação

**D10 — Taxa de deslocamento da fatura é ambígua quando há mais de um técnico na área.**
RN0071 manda somar "a taxa de deslocamento vigente para a área de atendimento em que o imóvel
está localizado", mas `AreaAtendimento` pertence a um `Tecnico`; dois técnicos podem cobrir o
mesmo CEP com taxas diferentes. *Proposta:* usar a `AreaAtendimento` do técnico efetivamente
atribuído ao chamado, cujo CEP contenha o imóvel. Confirmar.

**D11 — Conclusão de atendimento sem orçamento.**
RF0082 gera a fatura "a partir do orçamento aprovado". Não está dito se é possível concluir um
atendimento sem orçamento nenhum (serviço simples, sem peças). *Proposta:* permitir a
conclusão sem orçamento; a fatura sai com peças e mão de obra zeradas e apenas a taxa de
deslocamento. Alternativa: exigir orçamento aprovado para concluir. Confirmar.

**D12 — Garantia de chamado de garantia.**
RN0072 exige "fatura correspondente quitada" para acionar a garantia, mas chamados de garantia
não geram fatura. Um segundo acionamento sobre o atendimento de garantia não teria fatura para
verificar. *Proposta:* a garantia do atendimento de garantia herda a data de conclusão do
atendimento *original* para efeito dos 90 dias, e a checagem de quitação recai sobre a fatura
original. Confirmar.

**D13 — RN0052: como marcar a janela de avaliação como encerrada.**
O `EncerramentoJanelaAvaliacaoJob` "marca a janela como encerrada", mas não há campo para isso
no modelo. *Proposta:* derivar por data (`Agora > DataHoraConclusao + 15 dias`) no handler,
e o job apenas registra log e notifica, sem coluna nova. Isso evita estado redundante.
Confirmar; se for necessária a coluna, ela entra em `Chamado`.

**D14 — `POST /auth/refresh` sem entidade de refresh token.**
Não existe `RefreshToken` no modelo de domínio nem tabela prevista. *Proposta:* reemissão a
partir de um access token ainda válido (sem persistência), suficiente para o escopo e sem
inventar entidade fora do diagrama. Alternativa: criar tabela `refresh_token` na
infraestrutura. Confirmar.

**D15 — Origem do primeiro administrador.**
`POST /tecnicos` exige papel `Administrador` e não há endpoint público de cadastro de
administrador; o seed roda apenas em `Development`. Em qualquer outro ambiente não existe
caminho para criar o primeiro administrador. *Proposta:* comando de linha
`dotnet run --project src/ChamadosManutencao.Api -- criar-admin`, lendo credenciais de
variáveis de ambiente, executável uma única vez. Confirmar.

**D16 — Papel divergente em `GET /tecnicos/{id}/avaliacoes`.**
A tabela do UC02 restringe a `Administrador, Cliente`; a tabela do UC08 diz `autenticado`.
*Resolução proposta:* `autenticado`, que é o superconjunto e atende ao RF0062.

**D17 — `agruparPor` aceita apenas um eixo.**
RF0072 (filtrar por técnico) e RF0073 (selecionar categorias) sugerem comparação simultânea,
mas o parâmetro `agruparPor` da seção 10 aceita `categoria` **ou** `tecnico`. *Proposta:*
manter um único eixo de agrupamento por consulta, com filtro combinado nos dois campos
(agrupa por categoria, filtrando por técnicos selecionados, por exemplo). Confirmar.

**D18 — RF0074 pede "planilha", a especificação entrega CSV.**
CSV abre em qualquer planilha e evita dependência de biblioteca de XLSX. *Proposta:* manter
`text/csv` com `Content-Disposition: attachment`. Confirmar se é aceitável para a avaliação.

**D19 — `categoriaServicoId` redundante no corpo de `POST /chamados`.**
`TipoServico` já pertence a uma `CategoriaServico`. *Proposta:* aceitar os dois campos, como
manda a especificação, e validar coerência entre eles, devolvendo erro de validação quando o
tipo não pertencer à categoria informada.

**D20 — RN0031 contra RF0046 (administrador classifica urgência).**
A urgência `Alta` atribuída automaticamente por categoria de risco pode ser rebaixada depois
pelo administrador via `PATCH /chamados/{id}/urgencia`? *Proposta:* sim, permitir, gravando a
alteração no log de transação (RNF0012) — a classificação automática é o padrão inicial, não
uma trava. Confirmar.

**D21 — Anexos de documento do técnico compartilham a entidade `Anexo`.**
`Anexo.Origem` inclui `DocumentoTecnico`, mas no diagrama `Anexo` está ligado ao chamado.
*Proposta:* `Anexo` com `ChamadoId` e `TecnicoId` ambos anuláveis e uma `CHECK` garantindo que
exatamente um esteja preenchido.

**D22 — Pagamento de PIX e boleto é assíncrono no mundo real.**
O gateway simulado responde de forma síncrona. *Proposta:* `Pagamento` nasce `Processando` e o
gateway simulado resolve para `Aprovado`/`Recusado` na mesma chamada; a máquina de estados de
`Pagamento` já fica pronta para um fluxo assíncrono real.

**D23 — RNF0011 (1 segundo) não é testável de forma determinística.**
Testes de integração em Testcontainers não dão garantia de tempo. *Proposta:* o requisito é
atendido por construção (índices, paginação, `AsNoTracking`, projeção) e observado pelo
middleware de medição, que loga `Warning` acima de 1000 ms. Não haverá teste que falhe por
tempo.

### 10.4 Ambiente

**D24 — .NET SDK ausente na máquina.**
`dotnet` não está no PATH (verificado em bash e em PowerShell). Docker 29.1.3 está instalado e
funcional. A seção 3 pede aviso em vez de troca de versão por conta própria: fica registrado
que a Fase 1 em diante depende da instalação do **.NET 10 SDK**. A Fase 0 não é afetada.

---

## 11. Sequência de trabalho a partir daqui

| Fase | Entrega | Depende de |
|---|---|---|
| 0 | Este documento | — |
| 1 | Solution, projetos, Docker, README, `/health` | .NET 10 SDK (**D24**) |
| 2 | Domínio completo com testes das RN | Decisão sobre **D05**, **D06**, **D08** |
| 3 | Persistência, migrations, auditoria | Decisão sobre **D03**, **D21** |
| 4 | JWT, políticas, ProblemDetails, Serilog | Decisão sobre **D14**, **D15** |
| 5 | UC01, UC02, UC03 | Fase 4 |
| 6 | UC04, UC05, UC06, UC07 | Decisão sobre **D07**, **D09**, **D11** |
| 7 | UC08, UC09 | Decisão sobre **D13**, **D17**, **D18** |
| 8 | UC10, UC11, UC12 | Decisão sobre **D10**, **D12**, **D22** |
| 9 | Jobs, seed, rastreabilidade completa | Fases anteriores |
