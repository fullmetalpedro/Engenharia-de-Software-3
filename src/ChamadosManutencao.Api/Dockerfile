# Build a partir da raiz da solucao:
#   docker compose --profile full up -d --build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /origem

COPY Directory.Build.props ./
COPY ChamadosManutencao.sln ./
COPY src/ChamadosManutencao.Domain/*.csproj src/ChamadosManutencao.Domain/
COPY src/ChamadosManutencao.Application/*.csproj src/ChamadosManutencao.Application/
COPY src/ChamadosManutencao.Infrastructure/*.csproj src/ChamadosManutencao.Infrastructure/
COPY src/ChamadosManutencao.Api/*.csproj src/ChamadosManutencao.Api/
RUN dotnet restore src/ChamadosManutencao.Api/ChamadosManutencao.Api.csproj

COPY src/ src/
RUN dotnet publish src/ChamadosManutencao.Api/ChamadosManutencao.Api.csproj \
    -c Release -o /publicacao --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /publicacao ./
RUN mkdir -p /app/storage
EXPOSE 8080
ENTRYPOINT ["dotnet", "ChamadosManutencao.Api.dll"]
