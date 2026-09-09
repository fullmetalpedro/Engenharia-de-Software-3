# Sistema de Abertura de Chamados de Manutenção

Backend do sistema de abertura de chamados de manutenção de uma empresa de serviços
residenciais. API HTTP em .NET 10, PostgreSQL 17 em container, sem interface gráfica: a única
interface é a documentação OpenAPI servida por Scalar.

O projeto é *spec-driven*: cada requisito do DRS tem código que o implementa e teste que o
prova. O mapa completo está em [`docs/RASTREABILIDADE.md`](docs/RASTREABILIDADE.md).

| Documento | Conteúdo |
|---|---|
| [`docs/COMO_USAR.md`](docs/COMO_USAR.md) | Guia de operação: o que cada papel faz, na ordem em que faria no dia a dia |
| [`postman/README.md`](postman/README.md) | Collection do Postman: a interface para operar o sistema enquanto não há tela |
| [`docs/COMO_RODAR.md`](docs/COMO_RODAR.md) | Passo a passo para subir o projeto e rodar as três camadas de teste |
| [`docs/TESTES_MANUAIS.md`](docs/TESTES_MANUAIS.md) | Roteiro manual: 93 cenários com o comando, o resultado esperado e o observado |
| [`docs/ARQUITETURA.md`](docs/ARQUITETURA.md) | Desenho da solução, agregados, transversais e as dúvidas levantadas na análise |
| [`docs/DECISOES.md`](docs/DECISOES.md) | Registro das decisões tomadas, com a justificativa de cada uma |
| [`docs/RASTREABILIDADE.md`](docs/RASTREABILIDADE.md) | Os 78 requisitos com caso de uso, endpoint, classe e teste |

---

## Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://docs.docker.com/get-docker/) (para o banco e para os testes de integração,
  que usam Testcontainers)
- `dotnet-ef` para gerar e aplicar migrations:

```bash
dotnet tool install --global dotnet-ef
```

## Fluxo de desenvolvimento

```bash
cp .env.example .env
docker compose up -d postgres
dotnet ef database update -p src/ChamadosManutencao.Infrastructure -s src/ChamadosManutencao.Api
dotnet run --project src/ChamadosManutencao.Api
```

A API sobe em `http://localhost:5080`. A documentação fica em `http://localhost:5080/scalar`,
com os endpoints agrupados por caso de uso, e o health check em `http://localhost:5080/health`.

### Se a porta 5432 já estiver ocupada

Quando a máquina já tem um PostgreSQL nativo na 5432, troque a porta publicada no `.env`
(`POSTGRES_PORT=55432`, por exemplo) e aponte a aplicação para ela criando
`src/ChamadosManutencao.Api/appsettings.Local.json` — arquivo não versionado, lido logo depois
dos `appsettings` e antes das variáveis de ambiente:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=55432;Database=chamados;Username=chamados;Password=chamados_dev_senha"
  }
}
```

O `dotnet ef` não lê esse arquivo: passe a cadeia de conexão pelo ambiente na hora de aplicar as
migrations.

```bash
ConnectionStrings__Postgres="Host=localhost;Port=55432;Database=chamados;Username=chamados;Password=chamados_dev_senha" \
  dotnet ef database update -p src/ChamadosManutencao.Infrastructure -s src/ChamadosManutencao.Api
```

Em `Development` as migrations pendentes são aplicadas no startup; em qualquer outro ambiente
isso nunca acontece — o `dotnet ef database update` é obrigatório.

### Popular a base com dados de exemplo

```bash
dotnet run --project src/ChamadosManutencao.Api -- seed
```

O seed roda apenas em `Development` e é idempotente: rodar duas vezes não duplica nada. Ele
cria 1 administrador, 4 técnicos, 6 clientes com imóveis, 4 categorias de serviço, 40 chamados
distribuídos nos últimos 12 meses e faturas em todos os status.

### Criar o primeiro administrador fora de Development

```bash
dotnet run --project src/ChamadosManutencao.Api -- criar-admin
```

Lê `ADMIN_INICIAL_EMAIL`, `ADMIN_INICIAL_SENHA`, `ADMIN_INICIAL_CPF`, `ADMIN_INICIAL_NOME` e
`ADMIN_INICIAL_MATRICULA` do ambiente. É idempotente: se já existe administrador, não faz nada.

## Testes

```bash
dotnet test
```

Os testes de integração sobem um PostgreSQL descartável via Testcontainers, portanto o Docker
precisa estar rodando. Cobertura do domínio:

```bash
dotnet test tests/ChamadosManutencao.UnitTests --collect:"XPlat Code Coverage"
```

## Docker

| Comando | Efeito |
|---|---|
| `docker compose up -d postgres` | Sobe apenas o banco, com volume `chamados_pgdata` e healthcheck |
| `docker compose --profile tools up -d adminer` | Sobe o Adminer em `http://localhost:8080` para inspeção manual |
| `docker compose --profile full up -d --build` | Sobe banco e API em container, com volume para os anexos |
| `docker compose down` | Derruba os serviços preservando o volume |
| `docker compose down -v` | Derruba tudo e **apaga** o volume do banco |

## Estrutura

```
src/
  ChamadosManutencao.Domain/          entidades, invariantes, máquina de estados (sem dependências)
  ChamadosManutencao.Application/     handlers por caso de uso, validators, DTOs, abstrações
  ChamadosManutencao.Infrastructure/  EF Core, migrations, repositórios, jobs, serviços simulados
  ChamadosManutencao.Api/             Minimal APIs, segurança, middlewares
tests/
  ChamadosManutencao.UnitTests/       uma classe de teste por regra de negócio
  ChamadosManutencao.IntegrationTests/ endpoints reais contra PostgreSQL em Testcontainers
```

Dependências: `Api → Application → Domain` e `Infrastructure → Application → Domain`.

## Papéis e autenticação

Autenticação por JWT Bearer em `POST /api/v1/auth/login`. Três papéis: `Cliente`, `Tecnico` e
`Administrador`. Todos os endpoints, exceto login e cadastro de cliente, exigem token; os
endpoints de dono (chamado do cliente, atendimento do técnico) validam a propriedade além do
papel.
