# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for layer caching
COPY SearchEngine.slnx .
COPY src/SearchEngine.Domain/SearchEngine.Domain.csproj src/SearchEngine.Domain/
COPY src/SearchEngine.Application/SearchEngine.Application.csproj src/SearchEngine.Application/
COPY src/SearchEngine.Infrastructure/SearchEngine.Infrastructure.csproj src/SearchEngine.Infrastructure/
COPY src/SearchEngine.WebAPI/SearchEngine.WebAPI.csproj src/SearchEngine.WebAPI/

# Restore packages
RUN dotnet restore src/SearchEngine.WebAPI/SearchEngine.WebAPI.csproj

# Copy all source code
COPY src/ src/

# Publish
RUN dotnet publish src/SearchEngine.WebAPI/SearchEngine.WebAPI.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Npgsql Kerberos bagimliligini kur (libgssapi_krb5.so.2 uyarisini onlemek icin)
RUN apt-get update && apt-get install -y --no-install-recommends libkrb5-3 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SearchEngine.WebAPI.dll"]
