# Production image for U-PMS (research R25). No secrets are baked in: configuration comes from
# environment variables or the company secret store at run time.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ src/
RUN dotnet restore src/Upms.Web/Upms.Web.csproj
RUN dotnet publish src/Upms.Web/Upms.Web.csproj -c Release -o /app/publish --no-restore -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# The aspnet image ships a non-root "app" user; never run as root.
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Upms.Web.dll"]
