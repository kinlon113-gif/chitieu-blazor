FROM node:20-alpine AS web
WORKDIR /web

COPY ChiTieu.Web/package*.json ./
RUN npm ci

COPY ChiTieu.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ChiTieu.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore
COPY --from=web /web/dist /app/publish/wwwroot/react

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "ChiTieu.dll"]
