FROM mcr.microsoft.com/dotnet/aspnet:6.0

RUN apt-get update && \
    apt-get install -y xdg-utils && \
    apt-get clean

EXPOSE 80
EXPOSE 443

WORKDIR /app

COPY . .

ENTRYPOINT ["dotnet", "KindlerBot.dll"]