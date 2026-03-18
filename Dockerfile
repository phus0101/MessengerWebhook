# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj và restore
COPY ["src/MessengerWebhook/MessengerWebhook.csproj", "src/MessengerWebhook/"]
RUN dotnet restore "src/MessengerWebhook/MessengerWebhook.csproj"

# Copy source và build
COPY . .
WORKDIR "/src/src/MessengerWebhook"
RUN dotnet build "MessengerWebhook.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "MessengerWebhook.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080

# Non-root user
RUN adduser --disabled-password --gecos '' appuser
USER appuser

COPY --from=publish --chown=appuser:appuser /app/publish .
ENTRYPOINT ["dotnet", "MessengerWebhook.dll"]
