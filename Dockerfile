# 1. Build the React app into the API's wwwroot.
FROM node:22-alpine AS web
WORKDIR /src/src/TutoringManager.Web
COPY src/TutoringManager.Web/package.json src/TutoringManager.Web/package-lock.json ./
RUN npm ci
COPY src/TutoringManager.Web/ ./
RUN npm run build

# 2. Publish the API together with the built front end.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY TutoringManager.slnx Directory.Build.props ./
COPY src/TutoringManager.Api/TutoringManager.Api.csproj src/TutoringManager.Api/
RUN dotnet restore src/TutoringManager.Api/TutoringManager.Api.csproj
COPY src/TutoringManager.Api/ src/TutoringManager.Api/
COPY --from=web /src/src/TutoringManager.Api/wwwroot src/TutoringManager.Api/wwwroot
RUN dotnet publish src/TutoringManager.Api/TutoringManager.Api.csproj -c Release -o /app --no-restore

# 3. Small runtime image, non-root user.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "TutoringManager.Api.dll"]
