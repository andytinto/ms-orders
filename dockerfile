# =======================
# Stage 1: Build
# =======================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props ./

# Optimization: Copy solution and project files first to cache NuGet restore layers
COPY Orders.Domain/Orders.Domain.csproj ./Orders.Domain/
COPY Orders.Application/Orders.Application.csproj ./Orders.Application/
COPY Orders.Infrastructure/Orders.Infrastructure.csproj ./Orders.Infrastructure/
COPY Orders.Persistance/Orders.Persistance.csproj ./Orders.Persistance/
COPY Orders.Api/Orders.Api.csproj ./Orders.Api/

RUN dotnet restore "Orders.Api/Orders.Api.csproj"

# Copy all remaining source code files after restoring dependencies
COPY . .

# Publish main project
RUN dotnet publish "Orders.Api/Orders.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# =======================
# Stage 2: Runtime
# =======================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-composite-extra AS runtime
WORKDIR /app

# Copy built application artifacts
COPY --from=build /app/publish .

# Environment configuration
ENV ASPNETCORE_URLS=http://+:80 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

EXPOSE 80
ENTRYPOINT ["dotnet", "Orders.Api.dll"]
