# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MultiTenantSaaS.csproj", "./"]
RUN dotnet restore "MultiTenantSaaS.csproj"
COPY . .
RUN dotnet publish "MultiTenantSaaS.csproj" -c Release -o /app/publish

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 80
ENTRYPOINT ["dotnet", "MultiTenantSaaS.dll"]
