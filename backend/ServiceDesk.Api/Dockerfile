# Build context is the /backend folder (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ServiceDesk.Api/ServiceDesk.Api.csproj ServiceDesk.Api/
RUN dotnet restore ServiceDesk.Api/ServiceDesk.Api.csproj
COPY ServiceDesk.Api/ ServiceDesk.Api/
RUN dotnet publish ServiceDesk.Api/ServiceDesk.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/Uploads /app/logs && chown -R app:app /app/Uploads /app/logs
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "ServiceDesk.Api.dll"]
