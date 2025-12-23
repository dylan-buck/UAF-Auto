# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY SageBOI.sln .
COPY SageBOI.Api/SageBOI.Api.csproj SageBOI.Api/

# Restore dependencies
RUN dotnet restore SageBOI.sln

# Copy the rest of the source code
COPY . .

# Build the application
WORKDIR /src/SageBOI.Api
RUN dotnet build SageBOI.Api.csproj -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish SageBOI.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create logs directory
RUN mkdir -p /app/logs

# Copy published app
COPY --from=publish /app/publish .

EXPOSE 5000

ENTRYPOINT ["dotnet", "SageBOI.Api.dll"]

