# Como subir o projeto e rodar os testes

Guia prático para levantar a API na sua máquina, popular a base e executar as três camadas de
verificação: testes unitários, testes de integração e o roteiro manual.

---

## 1. Pré-requisitos

| Ferramenta | Versão | Para quê |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | 10 | compilar e rodar |
| [Docker](https://docs.docker.com/get-docker/) | qualquer versão recente | banco e testes de integração |
| `curl` | acompanha o Git Bash e o Windows 10+ | roteiro manual |
| `dotnet-ef` | 10 | gerar e aplicar migrations |

```bash
dotnet tool install --global dotnet-ef
```

Confira que está tudo no lugar:

```bash
dotnet --version     # 10.x
docker version       # o Docker precisa estar em execução
```

---

## 2. Conflito de porta 5432 (leia antes de subir o banco)

O `docker-compose.yml` publica o PostgreSQL na porta do host indicada por `POSTGRES_PORT`, que
por padrão é `5432`. **Se você tem um PostgreSQL instalado no Windows, ele já ocupa essa porta**
e o container sobe sem que a API consiga chegar nele — o sintoma é
`role "chamados" não existe`, porque a conexão cai no servidor errado.

Descubra se há um serviço concorrente:

```powershell
Get-Service | Where-Object { $_.DisplayName -like '*PostgreSQL*' }
netstat -ano | Select-String ":5432" | Select-String "LISTENING"
```

Você tem duas saídas.

**Opção A — parar o PostgreSQL do Windows** (exige terminal como administrador):

```powershell
Stop-Service postgresql-x64-18 -Force
Set-Service  postgresql-x64-18 -StartupType Manual   # não volta sozinho no próximo boot
```

**Opção B — publicar o container em outra porta.** No `.env`, troque a porta e aponte a cadeia
de conexão para ela:

```ini
POSTGRES_PORT=55432
```

```bash
export ConnectionStrings__Postgres="Host=localhost;Port=55432;Database=chamados;Username=chamados;Password=chamados_dev_senha"
```

O restante deste guia usa a porta padrão `5432`. Se escolher a opção B, troque a porta nos
comandos e mantenha a variável `ConnectionStrings__Postgres` exportada no terminal que roda a
API — ela tem precedência sobre o `appsettings.json`.

Os **testes de integração não são afetados** por esse conflito: o Testcontainers publica o
PostgreSQL numa porta aleatória livre.

---

## 3. Subir o ambiente

```bash
cp .env.example .env
docker compose up -d postgres
```

Espere o healthcheck ficar verde:

```bash
docker inspect -f '{{.State.Health.Status}}' chamados_postgres   # healthy
```

Aplique as migrations:

```bash
dotnet ef database update -p src/ChamadosManutencao.Infrastructure -s src/ChamadosManutencao.Api
```

Em `Development` a API também aplica as migrations pendentes no startup, então esse passo é
opcional no dia a dia — mas fora de `Development` ele é obrigatório.

### Popular a base com dados de exemplo

```bash
dotnet run --project src/ChamadosManutencao.Api -- seed
```

O seed roda **apenas em Development** e é idempotente: rodar duas vezes não duplica nada.
Ele cria:

- 1 administrador — `admin@chamados.local`
- 4 técnicos — `carlos.ribeiro@`, `marina.alves@`, `joao.batista@`, `fernanda.lima@chamados.local`
- 6 clientes — `ana.martins@`, `bruno.carvalho@`, `camila.nogueira@`, `diego.fonseca@`,
  `elaine.prado@`, `fabio.teixeira@exemplo.com`
- 4 categorias de serviço com seus tipos
- 40 chamados espalhados nos últimos 12 meses, em todos os seis status
- 15 faturas, cobrindo os quatro status

**A senha de todos os usuários do seed é `Senha@123`.**

### Rodar a API

```bash
dotnet run --project src/ChamadosManutencao.Api
```

| Endereço | O que é |
|---|---|
| `http://localhost:5080/health` | health check |
| `http://localhost:5080/scalar/` | documentação OpenAPI navegável, agrupada por caso de uso |
| `http://localhost:5080/openapi/v1.json` | o documento OpenAPI cru (68 operações) |

Os quatro jobs de segundo plano sobem junto e **executam uma vez logo no startup**, repetindo a
cada 15 minutos. Para desligá-los:

```bash
export Jobs__Habilitados=false
```

### Criar o primeiro administrador fora de Development

```bash
export ADMIN_INICIAL_EMAIL=admin@empresa.com
export ADMIN_INICIAL_SENHA='UmaSenha@Forte1'
export ADMIN_INICIAL_CPF=00000000191
export ADMIN_INICIAL_NOME='Administrador Inicial'
export ADMIN_INICIAL_MATRICULA=ADM-000001

dotnet run --project src/ChamadosManutencao.Api -- criar-admin
```

Idempotente: se já existe administrador, não faz nada.

---

## 4. Testes unitários

Não precisam de Docker nem de banco. Uma classe de teste por regra de negócio.

```bash
dotnet test tests/ChamadosManutencao.UnitTests
```

Esperado: **162 testes, todos passando**, em menos de um segundo.

Cobertura do domínio:

```bash
dotnet test tests/ChamadosManutencao.UnitTests --collect:"XPlat Code Coverage"
```

---

## 5. Testes de integração

Sobem um PostgreSQL 17 descartável via Testcontainers, aplicam as migrations e batem HTTP na
API hospedada em memória. **O Docker precisa estar rodando.**

```bash
dotnet test tests/ChamadosManutencao.IntegrationTests
```

Esperado: **154 testes, todos passando**, em torno de 3 a 4 minutos — a maior parte do tempo é
o BCrypt gerando hash de senha a cada cadastro.

Rodar só um caso de uso:

```bash
dotnet test tests/ChamadosManutencao.IntegrationTests --filter "Escopo=UC07"
dotnet test tests/ChamadosManutencao.IntegrationTests --filter "Escopo=Autorizacao"
dotnet test tests/ChamadosManutencao.IntegrationTests --filter "Escopo=Jornada"
```

Valores válidos de `Escopo`: `Autenticacao`, `UC01` a `UC12`, `Autorizacao`, `Jornada`,
`RNF0011`.

Rodar um teste específico:

```bash
dotnet test tests/ChamadosManutencao.IntegrationTests \
  --filter "FullyQualifiedName~Conclusao_gera_fatura_com_o_valor_do_orcamento_mais_a_taxa"
```

### Tudo de uma vez

```bash
dotnet test
```

---

## 6. Roteiro manual

O passo a passo com os comandos `curl` de cada cenário, o resultado esperado e o resultado
observado está em [`docs/TESTES_MANUAIS.md`](TESTES_MANUAIS.md). Use-o quando quiser conferir
o comportamento na API de verdade, e não no host de teste.

Para começar, com a API rodando e a base populada pelo seed:

```bash
BASE=http://localhost:5080/api/v1

TOKEN=$(curl -s -X POST $BASE/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin@chamados.local","senha":"Senha@123"}' \
  | python -c "import sys,json;print(json.load(sys.stdin)['accessToken'])")

curl -s -H "Authorization: Bearer $TOKEN" "$BASE/chamados?pageSize=5"
```

Pelo navegador é mais confortável: abra `http://localhost:5080/scalar/`, clique em
**Authentication**, cole o token e dispare as requisições direto da documentação.

---

## 7. Docker

| Comando | Efeito |
|---|---|
| `docker compose up -d postgres` | sobe só o banco, com volume `chamados_pgdata` e healthcheck |
| `docker compose --profile tools up -d adminer` | Adminer em `http://localhost:8080` para inspeção manual |
| `docker compose --profile full up -d --build` | sobe banco e API em container, com volume para os anexos |
| `docker compose down` | derruba os serviços preservando o volume |
| `docker compose down -v` | derruba tudo e **apaga** o volume do banco |

---

## 8. Quando algo dá errado

| Sintoma | Causa provável | O que fazer |
|---|---|---|
| `role "chamados" não existe` | outro PostgreSQL na porta 5432 | seção 2 deste guia |
| `Jwt:ChaveSecreta deve ser configurada com ao menos 32 caracteres` | `.env` não copiado, ou o terminal não carregou as variáveis | `cp .env.example .env` e reabra o terminal |
| Testes de integração param na criação do container | Docker parado ou imagem `postgres:17-alpine` ausente | `docker version`, depois `docker pull postgres:17-alpine` |
| Seed responde "A base já contém chamados" | o seed já rodou | é o comportamento idempotente; para recomeçar, `docker compose down -v` |
| A API sobe mas responde 500 em tudo | migrations não aplicadas fora de Development | rode o `dotnet ef database update` da seção 3 |

Para ler o que a API registrou, o log sai no console em formato estruturado. Toda escrita fica
gravada em `log_transacao` e toda notificação em `notificacao_enviada` — as duas tabelas são a
primeira parada de qualquer investigação:

```bash
docker exec chamados_postgres psql -U chamados -d chamados \
  -c "SELECT data_hora, entidade, operacao, usuario_id FROM log_transacao ORDER BY data_hora DESC LIMIT 10;"
```
