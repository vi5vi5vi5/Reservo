# Образ API. Многоэтапная сборка: SDK нужен только чтобы скомпилировать проект,
# в итоговый образ попадает лишь среда выполнения ASP.NET Core и опубликованное приложение.

# ---- Этап 1: сборка ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Сначала только файлы проектов и общие настройки: слой с restore кэшируется Docker
# и не пересобирается, пока не поменялись зависимости, а не при каждой правке кода.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Reservo.Domain/Reservo.Domain.csproj src/Reservo.Domain/
COPY src/Reservo.Application/Reservo.Application.csproj src/Reservo.Application/
COPY src/Reservo.Infrastructure/Reservo.Infrastructure.csproj src/Reservo.Infrastructure/
COPY src/Reservo.Api/Reservo.Api.csproj src/Reservo.Api/
RUN dotnet restore src/Reservo.Api/Reservo.Api.csproj

COPY src/ src/
RUN dotnet publish src/Reservo.Api/Reservo.Api.csproj --configuration Release --no-restore --output /app

# ---- Этап 2: итоговый образ ----
# Образ на Ubuntu: в нём есть ICU (русские названия месяцев в сообщениях) и база часовых поясов (Europe/Moscow).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Папка для файловых логов Serilog. Приложение работает не от root, поэтому папку заранее отдаём его пользователю.
RUN mkdir logs && chown "$APP_UID" logs

COPY --from=build /app .

# Непривилегированный пользователь из базового образа: даже при уязвимости в приложении у него нет прав root.
USER $APP_UID

# Базовый образ слушает HTTP на порту 8080 (ASPNETCORE_HTTP_PORTS). HTTPS даёт обратный прокси перед контейнером.
EXPOSE 8080
ENTRYPOINT ["dotnet", "Reservo.Api.dll"]
