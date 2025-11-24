# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY Contoso.Api.csproj .
RUN dotnet restore "Contoso.Api.csproj"

# Copy the rest of the source code
COPY . .

# Build the application
RUN dotnet build "Contoso.Api.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "Contoso.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Copy published output from publish stage
COPY --from=publish /app/publish .

# Set the entry point
ENTRYPOINT ["dotnet", "Contoso.Api.dll"]
