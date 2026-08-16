FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY *.sln .
COPY src/ActivityPub.Core/ActivityPub.Core.csproj src/ActivityPub.Core/
COPY src/ActivityPub.WebUI/ActivityPub.WebUI.csproj src/ActivityPub.WebUI/
RUN dotnet restore

COPY src/ActivityPub.Core/ src/ActivityPub.Core/
COPY src/ActivityPub.WebUI/ src/ActivityPub.WebUI/
WORKDIR /src/src/ActivityPub.WebUI
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=https://+:443;http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

RUN mkdir -p /data
VOLUME [ "/data" ]

EXPOSE 80 443

ENTRYPOINT ["dotnet", "ActivityPub.WebUI.dll"]
