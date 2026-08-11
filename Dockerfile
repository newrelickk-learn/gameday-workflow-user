# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY *.sln .
COPY src/UserService.Api/*.csproj ./src/UserService.Api/
COPY src/UserService.Application/*.csproj ./src/UserService.Application/
COPY src/UserService.Domain/*.csproj ./src/UserService.Domain/
COPY src/UserService.Infrastructure/*.csproj ./src/UserService.Infrastructure/

# Restore dependencies
RUN dotnet restore

# Copy all source files
COPY src/ ./src/

# Build and publish
WORKDIR /src/src/UserService.Api
RUN dotnet build -c Release -o /app/build
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 80

# Install wget for health checks
RUN apt-get update && apt-get install -y wget && rm -rf /var/lib/apt/lists/*

# Install the New Relic .NET Agent
RUN apt-get update && apt-get install -y wget ca-certificates gnupg \
    && echo 'deb http://apt.newrelic.com/debian/ newrelic non-free' | tee /etc/apt/sources.list.d/newrelic.list \
    && wget https://download.newrelic.com/548C16BF.gpg \
    && apt-key add 548C16BF.gpg \
    && apt-get update \
    && apt-get install -y newrelic-dotnet-agent \
    && rm -rf /var/lib/apt/lists/* 548C16BF.gpg

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "UserService.Api.dll"]

