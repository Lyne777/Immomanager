FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Immomanager.sln .
COPY src/Immomanager.Web/Immomanager.Web.csproj src/Immomanager.Web/
RUN dotnet restore src/Immomanager.Web/Immomanager.Web.csproj

COPY src/Immomanager.Web/ src/Immomanager.Web/
RUN dotnet publish src/Immomanager.Web/Immomanager.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
# Datenverzeichnis relativ zum Arbeitsverzeichnis - via Volume in docker-compose.yml persistent gemountet.
ENV Storage__DataDirectory=/app/data
RUN mkdir -p /app/data

EXPOSE 8080
VOLUME ["/app/data"]

ENTRYPOINT ["dotnet", "Immomanager.Web.dll"]
