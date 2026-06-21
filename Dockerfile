# Контейнер для хостингов вроде Render/Railway/Fly (альтернатива MonsterASP.NET).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/ForikAuction/ForikAuction.csproj
RUN dotnet publish src/ForikAuction/ForikAuction.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Render/контейнеры пробрасывают порт через переменную PORT; по умолчанию слушаем 8080.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ForikAuction.dll"]
