FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY MyAccountingApp.sln ./
COPY src/MyAccountingApp.Domain/MyAccountingApp.Domain.csproj src/MyAccountingApp.Domain/
COPY src/MyAccountingApp.Core/MyAccountingApp.Core.csproj src/MyAccountingApp.Core/
COPY src/MyAccountingApp.Application/MyAccountingApp.Application.csproj src/MyAccountingApp.Application/
COPY src/MyAccountingApp.Api/MyAccountingApp.Api.csproj src/MyAccountingApp.Api/
COPY src/MyAccountingApp.ConsoleApp/MyAccountingApp.ConsoleApp.csproj src/MyAccountingApp.ConsoleApp/
COPY tests/MyAccountingApp.TestUtilities/MyAccountingApp.TestUtilities.csproj tests/MyAccountingApp.TestUtilities/
COPY tests/MyAccountingApp.Domain.Tests/MyAccountingApp.Domain.Tests.csproj tests/MyAccountingApp.Domain.Tests/
COPY tests/MyAccountingApp.Core.Tests/MyAccountingApp.Core.Tests.csproj tests/MyAccountingApp.Core.Tests/
COPY tests/MyAccountingApp.Application.Tests/MyAccountingApp.Application.Tests.csproj tests/MyAccountingApp.Application.Tests/

RUN dotnet restore src/MyAccountingApp.Api/MyAccountingApp.Api.csproj

COPY . .
RUN dotnet publish src/MyAccountingApp.Api/MyAccountingApp.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080

COPY --from=build /app .

ENTRYPOINT ["MyAccountingApp.Api"]
