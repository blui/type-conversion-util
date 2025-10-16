# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["FileConversionApi/FileConversionApi.csproj", "FileConversionApi/"]
COPY ["FileConversionApi.Tests/FileConversionApi.Tests.csproj", "FileConversionApi.Tests/"]
RUN dotnet restore "FileConversionApi/FileConversionApi.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/FileConversionApi"
RUN dotnet build "FileConversionApi.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "FileConversionApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install system dependencies for LibreOffice and image processing
RUN apt-get update && apt-get install -y \
    libreoffice \
    libreoffice-java-common \
    default-jre \
    imagemagick \
    libmagickwand-dev \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser
RUN chown -R appuser:appuser /app
USER appuser

# Copy published app
COPY --from=publish /app/publish .

# Configure LibreOffice
ENV HOME=/tmp
ENV LIBREOFFICE_PATH=/usr/bin/libreoffice

# Create temp directories
RUN mkdir -p /tmp/uploads /tmp/converted /tmp/logs
VOLUME ["/tmp/uploads", "/tmp/converted", "/tmp/logs"]

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:3000/health || exit 1

# Expose port
EXPOSE 3000

# Set the entry point
ENTRYPOINT ["dotnet", "FileConversionApi.dll"]
