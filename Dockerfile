FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Immomanager.Web/Immomanager.Web.csproj src/Immomanager.Web/
RUN dotnet restore src/Immomanager.Web/Immomanager.Web.csproj

COPY src/Immomanager.Web/ src/Immomanager.Web/
# README.md liegt außerhalb von src/Immomanager.Web/ - wird separat kopiert, damit die
# "Anleitung"-Seite in der App sie auch im Container findet (siehe Documentation.razor).
COPY README.md .
RUN dotnet publish src/Immomanager.Web/Immomanager.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /src/README.md .

# Von der GitHub Action beim Build übergebene Commit-SHA - wird als APP_VERSION in der laufenden
# App sichtbar und ermöglicht den Versions-Check gegen den main-Branch (siehe VersionCheckService).
ARG GIT_SHA=unknown
ENV APP_VERSION=$GIT_SHA

ENV ASPNETCORE_URLS=http://+:8080
# Datenverzeichnis relativ zum Arbeitsverzeichnis - via Volume in docker-compose.yml persistent gemountet.
ENV Storage__DataDirectory=/app/data
RUN mkdir -p /app/data

EXPOSE 8080
VOLUME ["/app/data"]

ENTRYPOINT ["dotnet", "Immomanager.Web.dll"]
