FROM vps.zvirja.linkpc.net/aspnet-calibre:latest

EXPOSE 80
EXPOSE 443

WORKDIR /app

COPY . .

ENTRYPOINT ["dotnet", "KindlerBot.dll"]