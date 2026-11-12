# Imagem da API em quatro estágios: base (runtime), build, publish e final. As versões das imagens são fixas; o
# Dependabot as atualiza.

# Estágio base: só o runtime do ASP.NET; o Visual Studio usa este estágio para depurar no modo rápido.
FROM mcr.microsoft.com/dotnet/aspnet:11.0.0 AS base
# O curl serve só à sonda de saúde (HEALTHCHECK). A pasta dos anexos fica com o usuário da API, que não é root: sem
# isso, o envio de anexo e o health/ready falham no contêiner.
RUN apt-get update \
    && apt-get install --no-install-recommends -y curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/App_Data/attachments \
    && chown -R $APP_UID /app/App_Data
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# Estágio de build: restaura os pacotes e compila a API, com o SDK do global.json.
FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["global.json", "."]
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/TickestPristine.Web.Api/TickestPristine.Web.Api.csproj", "src/TickestPristine.Web.Api/"]
COPY ["src/TickestPristine.Infrastructure/TickestPristine.Infrastructure.csproj", "src/TickestPristine.Infrastructure/"]
COPY ["src/TickestPristine.Application/TickestPristine.Application.csproj", "src/TickestPristine.Application/"]
COPY ["src/TickestPristine.Domain/TickestPristine.Domain.csproj", "src/TickestPristine.Domain/"]
COPY ["src/TickestPristine.SharedKernel/TickestPristine.SharedKernel.csproj", "src/TickestPristine.SharedKernel/"]
RUN dotnet restore "./src/TickestPristine.Web.Api/TickestPristine.Web.Api.csproj"
COPY . .
WORKDIR "/src/src/TickestPristine.Web.Api"
RUN dotnet build "./TickestPristine.Web.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Estágio de publish: gera os arquivos copiados para o estágio final.
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./TickestPristine.Web.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Estágio final: imagem usada em produção, só com o runtime e a API publicada.
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Saudável quando o banco, a pasta dos anexos e, com o Redis configurado, o cache respondem (health/ready).
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "TickestPristine.Web.Api.dll"]
