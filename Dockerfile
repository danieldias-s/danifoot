FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Elifoot.slnx ./
COPY src/Elifoot.Contracts/Elifoot.Contracts.csproj src/Elifoot.Contracts/
COPY src/Elifoot.Domain/Elifoot.Domain.csproj src/Elifoot.Domain/
COPY src/Elifoot.Application/Elifoot.Application.csproj src/Elifoot.Application/
COPY src/Elifoot.Infrastructure/Elifoot.Infrastructure.csproj src/Elifoot.Infrastructure/
COPY src/Elifoot.Api/Elifoot.Api.csproj src/Elifoot.Api/
COPY tools/seed-generator/Elifoot.SeedGenerator.csproj tools/seed-generator/

RUN dotnet restore src/Elifoot.Api/Elifoot.Api.csproj

COPY . .
RUN dotnet publish src/Elifoot.Api/Elifoot.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5080
ENTRYPOINT ["dotnet", "Elifoot.Api.dll"]
