# Use the ASP.NET runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Copy pre-built publish artifacts
# Assumes publish folder is available at build context
COPY bin/Release/net8.0/publish/ .

# Set the entry point
ENTRYPOINT ["dotnet", "Contoso.Api.dll"]
