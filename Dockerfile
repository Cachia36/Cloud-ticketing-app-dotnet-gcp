FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["cloud-ticket-app.csproj", "."]
RUN dotnet restore "cloud-ticket-app.csproj"
COPY . .
RUN dotnet build "cloud-ticket-app.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "cloud-ticket-app.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
# Listen on 0.0.0.0:8080 inside the container
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

COPY --from=publish /app/publish .
# (Usually appsettings.json is already in publish; you can drop the next line)
# COPY appsettings.json ./appsettings.json

ENTRYPOINT ["dotnet", "cloud-ticket-app.dll"]
