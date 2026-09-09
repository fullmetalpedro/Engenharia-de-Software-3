# Decisões de projeto

Registro do que foi decidido durante a implementação, com a justificativa de cada escolha.
As dúvidas levantadas na Fase 0 estão em [`ARQUITETURA.md`](ARQUITETURA.md) com os mesmos
identificadores (D01 a D25); aqui está o que efetivamente entrou no código.

---

## Fontes e contradições dos artefatos

### D01 — O DRS de 78 requisitos é a soma de duas versões
O `.docx` entregue é a versão 0.1 (70 requisitos) e os 8 restantes (RF0081–RF0085, RN0071,
RN0072, RNF0061) aparecem apenas no PDF de diagramas versão 0.2 e na matriz de rastreabilidade.
**Decisão:** tratar DRS v0.1 + PDF v0.2 como fonte conjunta. A contagem fecha em 46 RF, 20 RN e
12 RNF.

### D02 — Identificadores são `Guid` v7, não `Integer`
O diagrama de classes declara `id : Integer` e `senha : String`. A especificação executável
exige `Guid` e `SenhaHash`, e o próprio prompt determina que a especificação vence.
**Decisão:** `Guid.CreateVersion7()` atrás de `IGeradorId` (ordenável no tempo, evita
fragmentação de índice) e senha somente como hash BCrypt.

### D03 — Nomes divergentes entre diagrama e especificação
`atendeRegiao(cep)` virou `AtendeCep(cep)`; `origemProposta : String` virou o enum
`OrigemProposta`. **Decisão:** prevalece a especificação; métodos que só existem no diagrama
(`Atendimento.iniciar`, `Pagamento.processar`) foram absorvidos pelos handlers correspondentes.

### D04 — Pacotes de teste fora da lista da seção 3
Foram adicionados `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, `coverlet.collector`
e `Microsoft.AspNetCore.Mvc.Testing`. **Justificativa:** são infraestrutura de teste (execução,
cobertura e `WebApplicationFactory`), sem efeito no código de produção. Nenhum outro pacote fora
da seção 3 foi incluído.

---

## Regras de negócio

### D05 — A máquina de estados amplia a RN0034 (bloqueante resolvido)
A RN0034 lista `CANCELADO` apenas a partir de `ABERTO`, `EM ANÁLISE` e `AGENDADO`, mas RN0042 e
RN0043 exigem cancelar durante o atendimento, e RN0035 exige voltar de `CONCLUÍDO` para
`EM ANÁLISE`. **Decisão:** a tabela de transições em `MaquinaDeEstadosDoChamado` inclui
`EM_ATENDIMENTO → CANCELADO` e `CONCLUIDO → EM_ANALISE`, justificadas por essas regras, mais
`AGENDADO → AGENDADO` (reagendamento) e `AGENDADO → EM_ANALISE`. Toda transição fora da tabela
lança `TransicaoDeStatusInvalidaException`.

### D06 — `EM ANÁLISE` é cancelável apenas pelo administrador
Reconcilia RN0033 (cliente cancela em `ABERTO` ou `AGENDADO`) com RN0034 (cancelamento também a
partir de `EM ANÁLISE`). **Decisão:** `Chamado.PodeSerCancelado()` cobre o caminho do cliente;
`CancelarPeloAdministrador` atende ao endpoint `/cancelamento-administrativo`.

### D07 — A abertura do chamado aceita as fotos na mesma requisição
Sem isso, a RN0032 tornaria impossível abrir chamado de categoria que exige foto, já que os
anexos entram por endpoint separado. **Decisão:** `POST /chamados` aceita `multipart/form-data`
com os arquivos junto dos campos (e continua aceitando JSON quando não há anexos). A validação
da foto acontece antes de qualquer gravação.

### D08 — Agendamento tem duração, com padrão de 120 minutos
O modelo original só tinha `dataHoraProposta`, e sem duração a RN0041 não consegue detectar
sobreposição. **Decisão:** `Agendamento.DuracaoEmMinutos`, padrão configurável em
`Agendamento:DuracaoPadraoEmMinutos`. Dois agendamentos conflitam quando as janelas se
sobrepõem; encostar no limite não é conflito.

### D09 — O limite de 5 anexos é contado por origem
O diagrama separa "ilustrado por (0..5)" das "fotos da conclusão (0..*)". **Decisão:**
`Chamado.AdicionarAnexo` conta por `OrigemAnexo`, então as fotos da conclusão não consomem a
cota das mídias de abertura. O teto de 10 MB por arquivo vale para todos.

### D10 — A taxa de deslocamento vem da área do técnico atribuído
`AreaAtendimento` pertence ao técnico, e dois técnicos podem cobrir o mesmo CEP com taxas
diferentes. **Decisão:** a fatura usa a área do técnico que executou o atendimento e cujo CEP
contém o imóvel (`Tecnico.AreaQueAtende`).

### D11 — Conclusão sem orçamento gera fatura só com a taxa
Serviço simples pode não ter peças nem mão de obra orçadas. **Decisão:** a conclusão é
permitida sem orçamento aprovado; nesse caso a fatura sai com peças e mão de obra zeradas e
apenas a taxa de deslocamento. Orçamento **pendente**, porém, bloqueia a conclusão (RN0043).

### D12 — Garantia de chamado de garantia olha a fatura original
Chamado de garantia não gera fatura, então a exigência de "fatura quitada" da RN0072 não teria
o que verificar num segundo acionamento. **Decisão:** quando o atendimento não tem fatura
própria, a checagem recai sobre a fatura do chamado que originou a cadeia.

### D13 — A janela de avaliação é derivada da data, sem coluna nova
**Decisão:** `Avaliacao.DentroDoPrazo(conclusao, agora)` calcula os 15 dias; o
`EncerramentoJanelaAvaliacaoJob` apenas registra e notifica, sem estado redundante no banco.

### D20 — O administrador pode reclassificar urgência definida pela RN0031
A prioridade `Alta` automática é o padrão inicial, não uma trava. A alteração fica registrada em
`log_transacao` (RNF0012).

---

## Modelo, segurança e API

### D14 — `refresh` reemite a partir de token válido
Não há entidade `RefreshToken` no diagrama. **Decisão:** `POST /auth/refresh` exige token
válido e reemite, sem persistir refresh tokens. Se a exigência mudar, entra uma tabela na
infraestrutura sem tocar no domínio.

### D15 — Primeiro administrador vem por comando de linha
Fora de `Development` não existe caminho para criar o primeiro administrador (o cadastro de
técnico exige administrador e o seed só roda em desenvolvimento). **Decisão:**
`dotnet run --project src/ChamadosManutencao.Api -- criar-admin`, lendo credenciais do ambiente
e idempotente.

### D16 — `GET /tecnicos/{id}/avaliacoes` é para qualquer autenticado
A especificação lista o endpoint duas vezes com papéis diferentes (UC02 e UC08). **Decisão:**
prevalece o superconjunto, `autenticado`, que atende ao RF0062.

### D17 — Um eixo de agrupamento por consulta
`agruparPor` aceita `categoria` **ou** `tecnico`, e os filtros de categoria e de técnico podem
ser combinados dentro do eixo escolhido.

### D18 — Exportação em CSV
RF0074 pede "formato de planilha". **Decisão:** `text/csv` com `Content-Disposition:
attachment`, que abre em qualquer planilha e evita dependência de biblioteca de XLSX.

### D19 — `categoriaServicoId` e `tipoServicoId` são validados entre si
O tipo já pertence a uma categoria, mas a especificação pede os dois campos no corpo da
abertura. **Decisão:** aceitar ambos e devolver erro de validação quando o tipo não pertencer à
categoria informada.

### D21 — `Anexo` serve ao chamado e ao técnico, nunca aos dois
FKs `chamado_id` e `tecnico_id` anuláveis, com `CHECK` garantindo que exatamente uma esteja
preenchida. As fábricas `Anexo.ParaChamado` e `Anexo.ParaTecnico` aplicam as regras de tipo MIME
de cada caso (RF0042 e RNF0032).

### D22 — Pagamento simulado responde de forma síncrona
`Pagamento` nasce com o status devolvido pelo gateway simulado. A máquina de estados
(`Processando`, `Aprovado`, `Recusado`, `Estornado`) já está pronta para um fluxo assíncrono
real de PIX e boleto. Para exercitar o caminho negativo, o gateway recusa valores terminados em
`.99`.

### D23 — RNF0011 é atendido por construção, não por teste de relógio
Índices, paginação obrigatória, `AsNoTracking` e projeção direta para DTO garantem o tempo; o
`MiddlewareDeTempoDeResposta` mede e emite log `Warning` acima de 1000 ms. `RNF0011TempoDeRespostaTests`
verifica o comportamento observável (paginação e teto de página) e uma folga confortável de
tempo, sem transformar latência de máquina de CI em falha de build.

### D24 — Concorrência otimista por `xmin` com coluna de sistema
O Npgsql 10 não expõe mais `UseXminAsConcurrencyToken()`. **Decisão:** mapear
`Property<uint>("xmin")` como token de concorrência e remover a coluna do `CREATE TABLE` na
migration inicial, já que `xmin` é coluna de sistema do PostgreSQL e existe em toda tabela.

### D25 — A `Application` referencia o pacote do EF Core
Para manter a projeção direta para DTO junto do caso de uso sem que a `Application` dependa da
`Infrastructure`, foi criada a abstração `IContextoDeLeitura`, implementada por
`ContextoDeLeitura` sobre o `AppDbContext`. Todas as propriedades já saem com `AsNoTracking`.
A direção das dependências entre projetos continua apontando para dentro.

---

## Registro de ambiente

- .NET SDK 10.0.400 e `dotnet-ef` 10.0.12 instalados durante a Fase 1.
- Docker Desktop 29.1.3 usado para o PostgreSQL 17 do compose e para os contêineres
  descartáveis dos testes de integração (Testcontainers).
