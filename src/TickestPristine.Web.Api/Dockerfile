# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
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

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./TickestPristine.Web.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TickestPristine.Web.Api.dll"]