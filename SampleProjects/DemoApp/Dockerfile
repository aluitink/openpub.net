# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy project files
COPY SampleProjects/DemoApp/DemoApp.csproj SampleProjects/DemoApp/
COPY ActivityPub.Core/ActivityPub.Core.csproj ActivityPub.Core/

# Restore dependencies
RUN cd SampleProjects/DemoApp && dotnet restore

# Copy remaining source
COPY . .

# Build and publish
RUN cd SampleProjects/DemoApp && dotnet build --no-restore -c Release
RUN cd SampleProjects/DemoApp && dotnet publish --no-restore -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
EXPOSE 8080

# Copy published files
COPY --from=build /app/publish .

# Run as non-root user
RUN adduser -D -g '' appuser && \
    chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "DemoApp.dll"]
