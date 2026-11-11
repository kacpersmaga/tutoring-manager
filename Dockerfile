FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY TutoringManager.slnx Directory.Build.props ./
COPY src/TutoringManager.Api/TutoringManager.Api.csproj src/TutoringManager.Api/
RUN dotnet restore src/TutoringManager.Api/TutoringManager.Api.csproj
COPY src/TutoringManager.Api/ src/TutoringManager.Api/
RUN dotnet publish src/TutoringManager.Api/TutoringManager.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TutoringManager.Api.dll"]
