FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY AegisId.sln global.json ./
COPY src/Aegis.Domain/Aegis.Domain.csproj src/Aegis.Domain/
COPY src/Aegis.Controls/Aegis.Controls.csproj src/Aegis.Controls/
COPY src/Aegis.Graph/Aegis.Graph.csproj src/Aegis.Graph/
COPY src/Aegis.Cli/Aegis.Cli.csproj src/Aegis.Cli/
COPY tests/Aegis.Domain.Tests/Aegis.Domain.Tests.csproj tests/Aegis.Domain.Tests/
COPY tests/Aegis.Graph.Tests/Aegis.Graph.Tests.csproj tests/Aegis.Graph.Tests/
COPY tests/Aegis.Cli.Tests/Aegis.Cli.Tests.csproj tests/Aegis.Cli.Tests/
RUN dotnet restore src/Aegis.Cli/Aegis.Cli.csproj

COPY . .
RUN dotnet publish src/Aegis.Cli/Aegis.Cli.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Aegis.Cli.dll"]
