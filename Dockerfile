FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG SERVICE=ChatApi
WORKDIR /src
COPY Directory.Build.props ./
COPY BookShop/BookShop.sln ./BookShop/
COPY BookShop/ ./BookShop/
COPY TelegramBot/ ./TelegramBot/
COPY ChatFSM/ ./ChatFSM/
COPY tests/ ./tests/
RUN dotnet restore BookShop/BookShop.sln
RUN dotnet publish BookShop/${SERVICE}/${SERVICE}.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ARG SERVICE
ENV SERVICE=${SERVICE}
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet ${SERVICE}.dll"]