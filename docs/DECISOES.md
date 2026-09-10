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

### D09 — O limite de 5 anexos é contado por origem ~~(revogada pela D29)~~
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

### D14 — `refresh` reemite a partir de token válido ~~(revogada pela D27)~~
Não há entidade `RefreshToken` no diagrama. **Decisão:** `POST /auth/refresh` exige token
válido e reemite, sem persistir refresh tokens.

**Revogada:** o endpoint saiu. Nenhum requisito menciona login, token ou sessão — o que o
diagrama traz é `Usuario.autenticar()`, que sustenta o login e não a reemissão. Ver D27.

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

## Decisões posteriores à análise

### D26 — Só o job que a RN0043 obriga permanece
A seção 8 da [`ARQUITETURA.md`](ARQUITETURA.md) previu quatro `BackgroundService`. Revendo a
origem de cada um contra o DRS, apenas um nasce de requisito:

| Job | Origem | Situação |
|---|---|---|
| `ExpiracaoOrcamentoJob` | **RN0043**: *"caso o prazo expire sem manifestação, o chamado deve ser automaticamente cancelado"* | mantido |
| `VencimentoFaturaJob` | nenhuma. `StatusFatura.VENCIDA` vem do enum do diagrama de classes e RF0083 só usa status como filtro de consulta | removido |
| `EncerramentoJanelaAvaliacaoJob` | RN0052 é verificada no momento do pedido; o job só contava e registrava | removido |
| `EncerramentoGarantiaJob` | RN0072 é verificada no acionamento; o job só contava e registrava | removido |

**Decisão:** remover os três. A palavra "automaticamente" da RN0043 é o que exige uma rotina
rodando sozinha; os demais prazos (RN0035, RN0052, RN0072) são checados na hora da operação e
recusam o pedido fora do prazo, comportamento coberto por teste. Manter processo de fundo sem
requisito é código que precisa de manutenção e não responde a ninguém.

**Consequência assumida:** `Fatura.RegistrarVencimento` e o evento `FaturaVencida` continuam no
domínio, mas hoje só o seed os aciona — nenhuma fatura passa a `Vencida` sozinha em produção.
Se o vencimento automático virar requisito, o job volta em uma classe.

### D27 — Fora o `criar-admin`, o que não tem requisito saiu
Auditoria dos 78 requisitos contra o código encontrou funcionalidades sem lastro em RF, RN ou
RNF. **Decisão:** remover, e registrar aqui o que cada uma custava.

| Removido | Por quê |
|---|---|
| `POST /auth/refresh` | o DRS não menciona login, token nem sessão; o diagrama traz `Usuario.autenticar()`, que sustenta o login, não a reemissão |
| `GET /health` | operacional, nascido da seção 11 da arquitetura |
| Cabeçalho `X-Tempo-De-Resposta-Ms` | acréscimo sobre a RNF0011; o log de aviso acima de 1000 ms fica |
| Campo `requisito` no ProblemDetails | rastreabilidade agradável, requisito nenhum |
| Paginação | nenhum requisito de consulta a pede; as listas voltam inteiras |
| Comando `seed` | ferramenta de demonstração |

**Mantido:** `criar-admin`. Onze requisitos pressupõem o ator administrador e nenhum documento
diz como ele passa a existir — a classe, a matrícula, a senha e o `ativo` estão no diagrama de
classes, só falta o ato de criar a primeira linha. Sem ele o sistema não se levanta fora de
`Development`. É bootstrap de infraestrutura, da mesma natureza de aplicar migration.

**Trocado:** Scalar por Swagger UI, a pedido. O documento OpenAPI continua sendo o do próprio
ASP.NET; mudou só quem o renderiza.

**Consequência assumida:** sem paginação, a consulta cresce com a base, o que pressiona a
RNF0011. O teto de 100 itens por página era proteção; a regra escrita fala em resposta de 1
segundo, não em página.

### D28 — Urgência não é dado de abertura
O RF0041 lista quatro dados que o cliente informa: imóvel, categoria, tipo de serviço e
descrição. O RF0046 dá a classificação de urgência ao administrador. O comando de abertura
aceitava um campo `urgencia` do cliente, que na prática deixava qualquer um furar a fila.
**Decisão:** o campo saiu. O chamado nasce `Media` e sobe para `Alta` sozinho apenas quando a
categoria é de risco e o cliente marca `indicacaoDeRisco` — o gatilho que a RN0031 exige.

### D29 — O limite de anexos é do chamado, não do momento ~~(revogada pela D38)~~
Revoga a D09. A RNF0043 fala em cinco arquivos **por chamado**; a cota separada por origem
permitia dez. **Decisão:** contar todos os anexos do chamado. A consequência é que fotos de
abertura consomem a cota das fotos de conclusão da RF0057 — tensão que está no próprio DRS, e
que resolvemos pela letra da RNF0043.

### D30 — O piso da RN0062 passou a reprovar
A validação contava meses inteiros de forma inclusiva, então o mínimo de 1 mês nunca barrava
nada. **Decisão:** comparar a distância entre as datas (`fim >= inicio.AddMonths(1)`), de modo
que um intervalo de dez dias seja recusado como a regra manda.

### D31 — Papel de cada endpoint segue o texto do requisito
Endpoints marcados como "qualquer autenticado" onde o requisito nomeia o papel foram
restringidos: reagendamento passa a ser do cliente ou do técnico (RF0053), e consulta de fatura,
pagamentos e garantia passam a ser do cliente dono ou do administrador (RF0083, RF0085) — o
técnico não tem assunto com fatura.

---

## Correcoes vindas do code review

### D32 — Um chamado reaberto precisa de mais de um atendimento
A RN0035 devolve o chamado para EM ANALISE, mas o indice `ix_atendimento_chamado_id` era
unico e o handler recusava qualquer segundo atendimento. Na pratica o chamado reaberto ficava
sem saida: podia ser agendado, nunca reconcluido. O diagrama de classes desenha
`Chamado 1 -- 0..1 Atendimento`, o que sustentava o indice.

**Decisao:** vale a regra funcional. O indice deixou de ser unico (migration
`PermitirVariosAtendimentosPorChamado`), o repositorio passou a devolver o atendimento mais
recente e a trava do inicio passou a barrar apenas atendimento **em andamento**. Mesma
natureza da D05: onde o texto do DRS e a cardinalidade do diagrama divergem, prevalece o
comportamento que o requisito descreve.

### D33 — A terceira condicao da RN0072 passou a comparar de verdade
O handler passava o mesmo identificador nos dois lados de "mesmo tipo de servico executado",
entao a condicao nunca reprovava. **Decisao:** `AcionarGarantiaCommand` aceita um
`tipoServicoId` opcional. Omitido, o chamado de garantia herda o tipo do original; informado e
diferente, a RN0072 recusa.

### D34 — Prazos contam da conclusao mais recente
Reabertura (RN0035) e janela de avaliacao (RN0052) resolviam a data de conclusao com
`SingleOrDefault` sobre os atendimentos encerrados. Com o chamado reaberto e reconcluido havia
mais de uma linha e a consulta estourava. **Decisao:** ordenar por data e usar a mais recente.

### D35 — A documentacao nao vai para producao, e a chave de desenvolvimento nao passa
O Swagger e o documento OpenAPI desenham o mapa das rotas e dos schemas para quem chega sem
token. **Decisao:** publicados apenas fora de `Production`. Junto, a chave JWT versionada no
`appsettings.json` passa a ser recusada no startup fora de `Development` — antes, esquecer a
variavel de ambiente subia o sistema com uma chave que esta no repositorio.

### D36 — Cartao vencido nao e recusado
`CartaoCredito.EstaValido(DateTimeOffset)` nao tinha chamador: o pagamento nunca olhava a
validade. Nenhum requisito pede essa recusa. **Decisao:** remover o metodo em vez de liga-lo,
pela mesma regra da D27. `Validar()` ficou so com o token da operadora, que e o que permite
cobrar (RNF0061).

### D37 — Agendamento valida antes de mexer no agregado
`Chamado.AdicionarAgendamento` marcava as propostas anteriores como reagendadas e adicionava a
nova antes de checar o status; a recusa vinha depois, com o agregado ja alterado em memoria.
**Decisao:** a verificacao de status subiu para o inicio do metodo.

### D38 — As fotos da conclusão pertencem ao atendimento
Revoga a D29. Contar tudo no chamado respeitava a letra da RNF0043, mas criava um beco: um
cliente que anexasse cinco fotos do problema impedia o técnico de concluir com foto alguma, e a
RF0057 pede exatamente essas fotos. O diagrama de classes já resolvia a tensão — `Chamado` é
"ilustrado por" `0..5` anexos e `Atendimento` tem "fotos da conclusão" `0..*`, associações
distintas. **Decisão:** a foto da conclusão é anexada ao atendimento (`Anexo.AtendimentoId`),
fica fora da cota de 5, e a cota da RNF0043 passa a valer para a mídia do problema, que é do
que a RF0042 trata. O teto de 10 MB por arquivo continua valendo para todos. `Anexo` agora tem
três donos possíveis e continua aceitando exatamente um (`ck_anexo_dono_unico`).

### D39 — Área de atendimento é opcional no cadastro do técnico
A RN0021 lista como obrigatórios nome completo, CPF, telefone, e-mail e **ao menos uma
especialidade** — a área de atendimento não está lá, e a RF0027 fala em "associar" regiões, não
em exigi-las. **Decisão:** o cadastro e a alteração aceitam a lista vazia. Sem área o técnico
simplesmente nunca passa na RN0023 e não chega a ser atribuído a chamado nenhum, que é o efeito
correto sem inventar obrigatoriedade.

### D40 — `AGENDADO` não volta para `EM ANÁLISE`
A tabela de transições permitia `AGENDADO → EM ANÁLISE`, sem que regra nenhuma pedisse. A
RN0034 não prevê a volta, e o reagendamento da RF0053 mantém o chamado em `AGENDADO`
(`AGENDADO → AGENDADO`), então a transição não tinha chamador. **Decisão:** removida da
`MaquinaDeEstadosDoChamado` e movida para a lista de transições inválidas do teste da RN0034.

---

## Registro de ambiente

- .NET SDK 10.0.400 e `dotnet-ef` 10.0.12 instalados durante a Fase 1.
- Docker Desktop 29.1.3 usado para o PostgreSQL 17 do compose e para os contêineres
  descartáveis dos testes de integração (Testcontainers).
