FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Chronicles.Api.csproj .
COPY modules/Chronicles.Domain/Chronicles.Domain.csproj modules/Chronicles.Domain/
COPY modules/Chronicles.Application/Chronicles.Application.csproj modules/Chronicles.Application/
COPY modules/Chronicles.Infrastructure/Chronicles.Infrastructure.csproj modules/Chronicles.Infrastructure/
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Chronicles.Api.dll"]
