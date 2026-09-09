# Como usar o sistema

Este documento é sobre **operar** o sistema, não sobre compilá-lo. Para subir o ambiente,
veja [`COMO_RODAR.md`](COMO_RODAR.md).

O sistema não tem tela. A interface é a API HTTP em `/api/v1`, e a forma mais confortável de
usá-la é o Scalar em `http://localhost:5080/scalar/`: os 68 endpoints agrupados por caso de
uso, com o corpo de cada requisição documentado e um botão para disparar a chamada ali mesmo.
Os exemplos aqui usam `curl` porque cabem no texto — no Scalar é o mesmo, sem digitar.

---

## Os três papéis

| Papel | Quem é | O que faz |
|---|---|---|
| **Cliente** | quem mora no imóvel | abre chamado, confirma agendamento, aprova orçamento, paga a fatura, avalia, aciona garantia |
| **Técnico** | quem executa o serviço | propõe horário, inicia o atendimento, registra orçamento, conclui o serviço |
| **Administrador** | a empresa | mantém catálogo e técnicos, tria chamados, atribui técnico, responde avaliações, analisa o histórico |

Todo endpoint, exceto o login e o cadastro de cliente, exige token. Além do papel, os
endpoints de dono checam a propriedade: o cliente só enxerga os próprios chamados, o técnico
só executa o atendimento que lhe foi atribuído.

---

## Colocando a operação de pé

Ordem importa: um chamado só existe se houver categoria, tipo de serviço, técnico habilitado e
cliente com imóvel. Faça uma vez, na implantação.

### 1. O primeiro administrador

Não há endpoint público para criar administrador — seria um buraco de segurança. Ele nasce por
linha de comando:

```bash
export ADMIN_INICIAL_EMAIL=admin@suaempresa.com.br
export ADMIN_INICIAL_SENHA='UmaSenha@Forte1'
export ADMIN_INICIAL_CPF=12345678901
export ADMIN_INICIAL_NOME='Regina Souza'
export ADMIN_INICIAL_MATRICULA=ADM-000001

dotnet run --project src/ChamadosManutencao.Api -- criar-admin
```

Os demais administradores entram pelo mesmo caminho.

### 2. Autenticar

```bash
BASE=http://localhost:5080/api/v1

curl -s -X POST $BASE/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"admin@suaempresa.com.br","senha":"UmaSenha@Forte1"}'
```

```json
{ "accessToken": "eyJhbGciOi...", "expiraEm": "2026-09-09T01:30:00+00:00", "papel": "Administrador" }
```

O token vale 60 minutos. `POST /auth/refresh` reemite sem pedir a senha de novo. Guarde em
variável:

```bash
ADMIN="Authorization: Bearer <accessToken>"
```

### 3. Montar o catálogo de serviços

Categoria é o guarda-chuva; tipo de serviço é o que o cliente escolhe na abertura.

```bash
curl -s -X POST $BASE/categorias-servico -H "$ADMIN" -H 'Content-Type: application/json' -d '{
  "nome": "Hidraulica",
  "descricao": "Reparos em tubulacoes, registros e loucas sanitarias.",
  "exigeFoto": true,
  "categoriaDeRisco": false
}'
```

Duas marcas mudam o comportamento do sistema e valem uma decisão consciente:

- **`exigeFoto`** — a abertura de chamado nessa categoria é recusada sem ao menos uma imagem.
  Ligue para eletrodomésticos e ar-condicionado, onde a foto do equipamento evita visita à toa.
- **`categoriaDeRisco`** — se o cliente marcar "indicação de risco" na abertura, a urgência vai
  automaticamente para **Alta**, independentemente do que ele escolheu. Ligue para elétrica e
  hidráulica, onde vazamento e curto-circuito não podem esperar na fila.

Depois, os tipos:

```bash
curl -s -X POST $BASE/categorias-servico/$CATEGORIA/tipos-servico \
  -H "$ADMIN" -H 'Content-Type: application/json' \
  -d '{"nome":"Reparo de vazamento","descricao":"Localizacao e reparo de vazamento."}'
```

### 4. Cadastrar a equipe

O técnico precisa de **especialidades** (as categorias que ele atende) e **áreas de
atendimento** (faixas de CEP, cada uma com sua taxa de deslocamento). Sem os dois ele não recebe
chamado nenhum.

```bash
curl -s -X POST $BASE/tecnicos -H "$ADMIN" -H 'Content-Type: application/json' -d '{
  "nomeCompleto": "Carlos Ribeiro",
  "cpf": "22233344455",
  "email": "carlos.ribeiro@suaempresa.com.br",
  "telefone": "11930000011",
  "senha": "Senha@123",
  "especialidadeIds": ["<id da categoria Hidraulica>"],
  "areasAtendimento": [
    { "bairro": "Centro",   "cepInicial": "01000000", "cepFinal": "01999999", "taxaDeslocamento": 25.00 },
    { "bairro": "Pinheiros","cepInicial": "05400000", "cepFinal": "05499999", "taxaDeslocamento": 35.00 }
  ]
}'
```

A **taxa de deslocamento é por área**, não por técnico: é ela que entra na fatura, escolhida
pela faixa de CEP onde o imóvel está. Cobrar mais caro por bairro distante é só cadastrar a área
com taxa maior.

O técnico recebe um código sequencial (`TEC-000001`) e já pode fazer login com a senha que você
definiu. Certificações vão em anexo, PDF ou imagem:

```bash
curl -s -X POST $BASE/tecnicos/$TECNICO/documentos -H "$ADMIN" \
  -F "arquivo=@certificado-nr10.pdf;type=application/pdf"
```

### 5. Os clientes se cadastram sozinhos

`POST /clientes` é anônimo — é o formulário de "criar conta". Exige nome, CPF, telefone,
e-mail, senha forte e **ao menos um imóvel**:

```bash
curl -s -X POST $BASE/clientes -H 'Content-Type: application/json' -d '{
  "nomeCompleto": "Ana Paula Martins",
  "cpf": "66677788899",
  "email": "ana.martins@exemplo.com",
  "telefone": "11940000001",
  "senha": "Senha@123",
  "imoveis": [{
    "apelido": "Apartamento do Centro",
    "tipoImovel": "Apartamento",
    "logradouro": "Rua Sete de Abril", "numero": "120", "complemento": "Apto 71",
    "bairro": "Centro", "cep": "01043000", "cidade": "Sao Paulo", "estado": "SP"
  }]
}'
```

Senha forte quer dizer 8 caracteres, maiúscula, minúscula e caractere especial. O cliente ganha
um código (`CLI-000001`) e o apelido do imóvel é o que ele vai ver na hora de abrir o chamado —
"Casa", "Apartamento da praia", "Loja".

---

## O dia a dia do cliente

### Abrir um chamado

```bash
curl -s -X POST $BASE/chamados -H "$CLIENTE" \
  -F "imovelId=$IMOVEL" \
  -F "categoriaServicoId=$CATEGORIA" \
  -F "tipoServicoId=$TIPO" \
  -F "descricaoProblema=Vazamento embaixo da pia da cozinha desde ontem" \
  -F "indicacaoDeRisco=true" \
  -F "urgencia=Media" \
  -F "arquivos=@foto-do-vazamento.jpg;type=image/jpeg"
```

Sem foto, dá para mandar JSON puro no mesmo endereço. Com foto — obrigatória nas categorias
marcadas com `exigeFoto` — vai `multipart/form-data`, e a foto sobe **na mesma requisição** da
abertura: não existe um chamado meio-criado esperando anexo.

A resposta traz o número sequencial que o cliente usa para falar com a empresa:

```json
{ "id": "01a08391-...", "numero": 41, "status": "Aberto", "urgencia": "Alta" }
```

Note a urgência: o cliente pediu `Media`, mas marcou risco numa categoria de risco, e o sistema
subiu para `Alta`.

Limite de 5 arquivos por chamado, 10 MB cada. Mais fotos depois:

```bash
curl -s -X POST $BASE/chamados/$CHAMADO/anexos -H "$CLIENTE" -F "arquivo=@outra-foto.jpg"
```

### Acompanhar

```bash
curl -s -H "$CLIENTE" "$BASE/chamados/meus?status=Agendado"
curl -s -H "$CLIENTE" "$BASE/chamados/$CHAMADO/historico-status"
```

O histórico mostra cada transição com data, hora, responsável e observação — é a resposta para
"o que aconteceu com o meu chamado".

### Confirmar o horário

Quando o técnico propõe, o cliente confirma:

```bash
curl -s -H "$CLIENTE" $BASE/chamados/$CHAMADO/agendamentos     # ver o que foi proposto
curl -s -X POST $BASE/agendamentos/$AGENDAMENTO/confirmacao -H "$CLIENTE"
```

Não deu para o horário proposto? Reagenda, e o anterior fica marcado como `Reagendado` no
histórico:

```bash
curl -s -X POST $BASE/agendamentos/$AGENDAMENTO/reagendamento \
  -H "$CLIENTE" -H 'Content-Type: application/json' \
  -d '{"dataHoraProposta":"2026-09-15T14:00:00Z","duracaoEmMinutos":120}'
```

### Aprovar ou recusar o orçamento

O técnico chega, avalia e registra o orçamento. **O relógio de 48 horas começa a correr.**

```bash
curl -s -H "$CLIENTE" $BASE/atendimentos/$ATENDIMENTO/orcamento

curl -s -X POST $BASE/orcamentos/$ORCAMENTO/decisao \
  -H "$CLIENTE" -H 'Content-Type: application/json' \
  -d '{"decisao":"Aprovado"}'
```

Três caminhos, e a diferença entre eles é grande:

| Decisão | Corpo | O que acontece |
|---|---|---|
| Aprovar | `{"decisao":"Aprovado"}` | o técnico segue e conclui |
| Recusar | `{"decisao":"Recusado"}` | **o chamado é cancelado** |
| Recusar pedindo outro | `{"decisao":"Recusado","solicitarNovoOrcamento":true}` | o chamado continua em atendimento, o técnico registra novo orçamento |
| Não responder em 48 h | — | o chamado é **cancelado automaticamente** |

### Cadastrar forma de pagamento

Antes de pagar a primeira fatura. Cartão, PIX ou boleto:

```bash
curl -s -X POST $BASE/clientes/$CLIENTE_ID/formas-pagamento \
  -H "$CLIENTE" -H 'Content-Type: application/json' -d '{
    "tipo": "CartaoCredito", "apelido": "Cartao principal", "principal": true,
    "numeroCartao": "4111111111111111", "nomeTitular": "ANA P MARTINS",
    "validade": "12/2030", "codigoSeguranca": "123"
  }'
```

O número completo e o CVV **não são guardados**. O sistema tokeniza e persiste apenas token,
bandeira, quatro últimos dígitos e validade. A resposta reflete isso:

```json
{ "tipo": "CartaoCredito", "descricao": "Visa **** 1111", "principal": true, "validade": "12/2030" }
```

Exatamente uma forma fica como principal por vez. A exclusão é lógica — a forma some das
opções mas o histórico de pagamentos continua íntegro.

### Pagar

A fatura nasce sozinha quando o técnico conclui o serviço.

```bash
curl -s -H "$CLIENTE" "$BASE/faturas/minhas?status=Emitida"

curl -s -X POST $BASE/faturas/$FATURA/pagamentos \
  -H "$CLIENTE" -H 'Content-Type: application/json' \
  -d "{\"formaPagamentoId\":\"$FORMA\"}"
```

Vencimento em 15 dias a partir da emissão. Passou, o status vira `Vencida` sozinho.

### Avaliar e acionar garantia

Nota de 1 a 5, **em até 15 dias corridos** após a conclusão:

```bash
curl -s -X POST $BASE/chamados/$CHAMADO/avaliacao \
  -H "$CLIENTE" -H 'Content-Type: application/json' \
  -d '{"nota":5,"comentario":"Chegou no horario e resolveu."}'
```

Todo serviço concluído sai com **90 dias de garantia**. Se o problema voltar:

```bash
curl -s -X POST $BASE/atendimentos/$ATENDIMENTO/garantia/acionamento \
  -H "$CLIENTE" -H 'Content-Type: application/json' \
  -d '{"descricaoProblema":"O vazamento voltou na mesma tubulacao"}'
```

Nasce um chamado novo, marcado como chamado de garantia, urgência Alta, vinculado ao original,
já atribuído ao técnico que fez o serviço — e **sem fatura no fim**. Três condições: dentro dos
90 dias, fatura do serviço original quitada, e mesmo tipo de serviço.

Se o serviço não resolveu de primeira e faz menos de 7 dias, existe um caminho mais curto:
reabrir o próprio chamado, que volta para EM ANÁLISE sem gerar chamado novo.

```bash
curl -s -X POST $BASE/chamados/$CHAMADO/reabertura -H "$CLIENTE"
```

---

## O dia a dia do técnico

O técnico entra no sistema depois que o administrador o atribui a um chamado.

### 1. Propor o horário

```bash
curl -s -X POST $BASE/chamados/$CHAMADO/agendamentos \
  -H "$TECNICO" -H 'Content-Type: application/json' \
  -d '{"dataHoraProposta":"2026-09-10T09:00:00Z","duracaoEmMinutos":120}'
```

O sistema recusa se o horário colidir com outro atendimento seu. A duração importa: é ela que
define a janela ocupada na agenda.

### 2. Iniciar no local

```bash
curl -s -X POST $BASE/chamados/$CHAMADO/atendimento -H "$TECNICO"
```

Marca a hora de chegada e leva o chamado para EM ATENDIMENTO. Só o técnico atribuído consegue.

### 3. Registrar o orçamento

Peças e mão de obra, item a item:

```bash
curl -s -X POST $BASE/atendimentos/$ATENDIMENTO/orcamento \
  -H "$TECNICO" -H 'Content-Type: application/json' -d '{
    "itens": [
      {"descricao":"Registro de gaveta 1/2","tipo":"Peca","quantidade":1,"valorUnitario":90.00},
      {"descricao":"Mao de obra","tipo":"MaoDeObra","quantidade":2,"valorUnitario":75.00}
    ]
  }'
```

Serviço simples que não precisa de peça? Pule o orçamento e conclua direto — a fatura sai
com peças e mão de obra zeradas, cobrando só a taxa de deslocamento da área.

### 4. Concluir

```bash
curl -s -X POST $BASE/atendimentos/$ATENDIMENTO/conclusao -H "$TECNICO" \
  -F "relatoTecnico=Registro trocado e teste de pressao aprovado." \
  -F "arquivos=@servico-finalizado.jpg;type=image/jpeg"
```

Essa é a chamada que mais faz coisa acontecer:

- o chamado vai para CONCLUÍDO
- abre a garantia de 90 dias
- **a fatura é emitida**, somando peças + mão de obra + taxa de deslocamento da área do imóvel
- o cliente é notificado e convidado a avaliar

Se houver orçamento pendente de decisão, a conclusão é barrada — resolva a pendência antes.

---

## O dia a dia do administrador

### Triagem

A fila de trabalho:

```bash
curl -s -H "$ADMIN" "$BASE/chamados?status=Aberto&urgencia=Alta"
```

Filtros combinam livremente: `status`, `urgencia`, `categoriaId`, `tecnicoId`, `clienteId`,
`dataInicio`, `dataFim`, `numero`.

Ajustar a prioridade quando a automática não refletiu a realidade:

```bash
curl -s -X PATCH $BASE/chamados/$CHAMADO/urgencia \
  -H "$ADMIN" -H 'Content-Type: application/json' -d '{"urgencia":"Alta"}'
```

Atribuir o técnico — o passo que tira o chamado de ABERTO:

```bash
curl -s -X POST $BASE/chamados/$CHAMADO/atribuicao \
  -H "$ADMIN" -H 'Content-Type: application/json' -d "{\"tecnicoId\":\"$TECNICO\"}"
```

O sistema recusa se o técnico não tiver a categoria entre as especialidades, se o imóvel estiver
fora das áreas dele, ou se ele estiver inativo. Trocar de técnico depois é o mesmo endereço com
`PUT`.

Cancelar depois que o atendimento já começou é prerrogativa do administrador:

```bash
curl -s -X POST $BASE/chamados/$CHAMADO/cancelamento-administrativo \
  -H "$ADMIN" -H 'Content-Type: application/json' \
  -d '{"motivo":"Cliente pediu cancelamento por telefone."}'
```

### Responder avaliações

Nota baixa merece resposta pública:

```bash
curl -s -H "$ADMIN" "$BASE/tecnicos/$TECNICO/avaliacoes"

curl -s -X POST $BASE/avaliacoes/$AVALIACAO/resposta \
  -H "$ADMIN" -H 'Content-Type: application/json' \
  -d '{"texto":"Lamentamos o atraso e ja ajustamos a rota da equipe."}'
```

### Analisar o histórico

Quantos chamados por mês, por categoria ou por técnico, no período que você escolher:

```bash
curl -s -H "$ADMIN" "$BASE/analises/chamados?dataInicio=2025-09-01T00:00:00Z&dataFim=2026-09-01T00:00:00Z"
```

```json
{
  "periodo": { "inicio": "2025-09", "fim": "2026-09" },
  "eixoX": ["2025-09", "2025-10", "..."],
  "series": [
    { "legenda": "Eletrica",   "valores": [1, 3, 3, "..."] },
    { "legenda": "Hidraulica", "valores": [0, 1, 1, "..."] }
  ],
  "totalChamados": 39
}
```

A resposta já vem no formato do gráfico de linhas: eixo X é mês/ano, cada série é uma linha.
Chamados cancelados não entram na conta. O intervalo vai de 1 a 24 meses.

Comparar desempenho entre técnicos, ou olhar só duas categorias:

```bash
curl -s -H "$ADMIN" "$BASE/analises/chamados?dataInicio=...&dataFim=...&agruparPor=tecnico&tecnicoIds=$T1&tecnicoIds=$T2"
curl -s -H "$ADMIN" "$BASE/analises/chamados?dataInicio=...&dataFim=...&categoriaIds=$C1&categoriaIds=$C2"
```

Para levar para a reunião, exporte em planilha:

```bash
curl -s -OJ -H "$ADMIN" "$BASE/analises/chamados/exportacao?dataInicio=...&dataFim=..."
```

Sai um CSV com ponto e vírgula, que abre direto no Excel:

```csv
serie;2025-09;2025-10;...;total
Eletrica;1;3;...;30
Hidraulica;0;1;...;9
```

### Manutenção do cadastro

Cliente que saiu, técnico que se desligou: **inative, não apague**. Inativo não faz login e não
recebe chamado, mas o histórico continua de pé.

```bash
curl -s -X PATCH $BASE/clientes/$CLIENTE_ID/inativacao -H "$ADMIN"
curl -s -X PATCH $BASE/tecnicos/$TECNICO/inativacao   -H "$ADMIN"
```

---

## O que acontece sozinho

Quatro rotinas rodam a cada 15 minutos, e uma vez ao subir a aplicação:

| Rotina | O que faz |
|---|---|
| Expiração de orçamento | passou de 48 h sem decisão: expira o orçamento e cancela o chamado |
| Vencimento de fatura | fatura emitida que passou do vencimento vira `Vencida` e notifica o cliente |
| Encerramento da janela de avaliação | registra quantas janelas de 15 dias fecharam sem nota |
| Encerramento de garantia | registra quantas garantias de 90 dias venceram no intervalo |

Além disso, **toda mudança de status notifica o cliente** e **toda escrita é auditada**. As duas
coisas ficam em tabela e são a primeira parada de qualquer investigação:

```sql
SELECT data_hora, assunto FROM notificacao_enviada ORDER BY data_hora DESC LIMIT 10;

SELECT data_hora, entidade, operacao, usuario_id, estado_anterior, estado_novo
FROM log_transacao WHERE entidade = 'Chamado' ORDER BY data_hora DESC LIMIT 10;
```

Senha e token de cartão aparecem no log como `"***"` — nunca em claro.

---

## Quando o sistema diz não

Erros de negócio vêm em ProblemDetails com o campo `requisito`, que aponta a regra violada.
Isso é de propósito: dá para mostrar a mensagem ao usuário e rastrear a origem no DRS.

```json
{
  "title": "Regra de negocio violada",
  "status": 422,
  "detail": "O tecnico nao possui a categoria do chamado entre suas especialidades.",
  "requisito": "RN0022"
}
```

| Você tentou | Resposta | Regra | Saída |
|---|---|---|---|
| Abrir chamado sem foto em categoria que exige | 409 `RN0032` | foto obrigatória | anexe a foto na mesma requisição |
| Atribuir técnico sem a especialidade | 422 `RN0022` | especialidade | escolha outro técnico ou acrescente a categoria a ele |
| Atribuir técnico fora da área | 422 `RN0023` | área de atendimento | cadastre a faixa de CEP para ele |
| Atribuir a técnico inativo | 422 `RF0023` | inativo não atende | reative antes |
| Agendar em horário ocupado | 409 `RN0041` | agenda do técnico | escolha outra janela |
| Concluir com orçamento pendente | 422 `RN0043` | decisão do cliente | espere a aprovação ou a expiração |
| Cancelar chamado já em atendimento | 422 `RN0033` | só ABERTO ou AGENDADO | use o cancelamento administrativo |
| Pular status (ABERTO → CONCLUÍDO) | 422 `RN0034` | fluxo de status | siga a sequência |
| Reabrir depois de 7 dias | 422 `RN0035` | prazo de reabertura | acione a garantia |
| Avaliar depois de 15 dias | 409 `RN0052` | janela de avaliação | a janela fechou |
| Acionar garantia com fatura em aberto | 409 `RN0072` | fatura quitada | pague a fatura primeiro |
| Remover o último imóvel do cliente | 422 `RN0011` | ao menos um imóvel | cadastre outro antes |
| Sexto anexo no chamado | 422 `RNF0043` | 5 arquivos por chamado | remova ou agrupe |

Fora isso: **401** é token ausente, expirado ou inválido; **403** é papel errado ou recurso de
outra pessoa; **400** é corpo ou parâmetro malformado, com a lista de campos em `errors`.

O fluxo de status, para consulta rápida:

```
ABERTO ──► EM ANÁLISE ──► AGENDADO ──► EM ATENDIMENTO ──► CONCLUÍDO
   │            │             │               │                │
   └────────────┴─────────────┴───────────────┴──► CANCELADO    │
                                                                │
                            EM ANÁLISE ◄── reabertura (7 dias) ─┘
```

---

## O que ainda não é real

Honestidade sobre os limites desta versão, para você não descobrir em produção:

- **Notificação (RNF0041) não sai da máquina.** O notificador grava log estruturado e uma linha
  em `notificacao_enviada`. Não há e-mail nem push. A troca por SMTP ou push é implementar
  `INotificador` — o domínio não muda.
- **O gateway de pagamento é simulado.** Nenhuma cobrança real acontece. A tokenização é
  fingida e a autorização aprova tudo, com uma exceção proposital para você testar recusa:
  **valor terminado em `,99` é recusado** com "saldo insuficiente". Útil para exercitar o
  caminho triste sem tocar em operadora de verdade.
- **Anexos sobem, mas não descem.** Os arquivos ficam num volume local sob `storage/` e há
  endpoint de upload para chamado e para documento do técnico — mas ainda **não existe endpoint
  de download**. Hoje o arquivo é recuperável só pelo volume.
- **Boleto é só cadastro.** O tipo existe como forma de pagamento; não há emissão de boleto,
  linha digitável nem baixa por retorno bancário.
- **Não há exclusão de cadastro.** Cliente e técnico são inativados, nunca removidos — decisão
  para preservar o histórico de chamados e faturas.

---

## Um roteiro para conhecer o sistema em 10 minutos

Com o seed aplicado (`dotnet run --project src/ChamadosManutencao.Api -- seed`) e a senha
`Senha@123` valendo para todos:

1. Entre como `admin@chamados.local` e veja a fila: `GET /chamados?status=Aberto`
2. Veja o gráfico de um ano: `GET /analises/chamados?dataInicio=...&dataFim=...`
3. Entre como `ana.martins@exemplo.com` e abra um chamado com foto
4. Volte ao administrador e atribua `carlos.ribeiro@chamados.local`
5. Entre como o técnico, proponha horário, e confirme como a cliente
6. Como técnico: inicie, registre orçamento, conclua
7. Como cliente: veja a fatura que apareceu, cadastre um PIX e pague
8. Avalie com nota 5; volte como administrador e responda
9. Acione a garantia e veja o chamado novo nascer vinculado ao original
10. Termine olhando `GET /chamados/{id}/historico-status` — a jornada inteira, registrada

O passo a passo detalhado dessa sequência, com o resultado observado em cada chamada, está em
[`TESTES_MANUAIS.md`](TESTES_MANUAIS.md).
