FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first (layer cache for restore)
COPY ["TravelManagement_API/TravelManagement_API.csproj", "TravelManagement_API/"]
COPY ["TravelManagement_BusinessLogicLayer/TravelManagement_BusinessLogicLayer.csproj", "TravelManagement_BusinessLogicLayer/"]
COPY ["TravelManagement_Core/TravelManagement_Core.csproj", "TravelManagement_Core/"]
COPY ["TravelManagement_DataAccessLayer/TravelManagement_DataAccessLayer.csproj", "TravelManagement_DataAccessLayer/"]

RUN dotnet restore "TravelManagement_API/TravelManagement_API.csproj"

# Copy everything else and publish
COPY . .

RUN dotnet publish "TravelManagement_API/TravelManagement_API.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "TravelManagement_API.dll"]
