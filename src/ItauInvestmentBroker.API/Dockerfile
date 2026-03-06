FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ItauInvestmentBroker.sln ./
COPY src/ItauInvestmentBroker.API/ItauInvestmentBroker.API.csproj src/ItauInvestmentBroker.API/
COPY src/ItauInvestmentBroker.Application/ItauInvestmentBroker.Application.csproj src/ItauInvestmentBroker.Application/
COPY src/ItauInvestmentBroker.Domain/ItauInvestmentBroker.Domain.csproj src/ItauInvestmentBroker.Domain/
COPY src/ItauInvestmentBroker.Infrastructure/ItauInvestmentBroker.Infrastructure.csproj src/ItauInvestmentBroker.Infrastructure/

RUN dotnet restore src/ItauInvestmentBroker.API/ItauInvestmentBroker.API.csproj

COPY . .
RUN dotnet publish src/ItauInvestmentBroker.API/ItauInvestmentBroker.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ItauInvestmentBroker.API.dll"]
