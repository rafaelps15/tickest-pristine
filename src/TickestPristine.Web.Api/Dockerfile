# Imagem da API em quatro estágios: base (runtime), build, publish e final.

# Estágio base: só o runtime do ASP.NET; o Visual Studio usa este estágio para depurar no modo rápido.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# Estágio de build: restaura os pacotes e compila a API.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
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
ENTRYPOINT ["dotnet", "TickestPristine.Web.Api.dll"]