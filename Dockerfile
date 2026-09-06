FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/GitHubRefollow/GitHubRefollow.csproj src/GitHubRefollow/
RUN dotnet restore src/GitHubRefollow/GitHubRefollow.csproj

COPY src/GitHubRefollow/ src/GitHubRefollow/
RUN dotnet publish src/GitHubRefollow/GitHubRefollow.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

RUN mkdir -p /data && chown -R app:app /data
COPY --from=build --chown=app:app /app/publish .

USER app
ENTRYPOINT ["dotnet", "GitHubRefollow.dll"]
