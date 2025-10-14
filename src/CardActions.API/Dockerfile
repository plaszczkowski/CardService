FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/CardActions.API/CardActions.API.csproj", "src/CardActions.API/"]
COPY ["src/CardActions.Application/CardActions.Application.csproj", "src/CardActions.Application/"]
COPY ["src/CardActions.Domain/CardActions.Domain.csproj", "src/CardActions.Domain/"]
COPY ["src/CardActions.Infrastructure/CardActions.Infrastructure.csproj", "src/CardActions.Infrastructure/"]
RUN dotnet restore "src/CardActions.API/CardActions.API.csproj"


COPY . .
WORKDIR "/src/src/CardActions.API"
RUN dotnet build "CardActions.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CardActions.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create non-root user for security
RUN groupadd -r appgroup && useradd -r -g appgroup appuser
RUN chown -R appuser:appgroup /app
USER appuser

ENTRYPOINT ["dotnet", "CardActions.API.dll"]