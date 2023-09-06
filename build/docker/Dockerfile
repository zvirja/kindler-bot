FROM mcr.microsoft.com/dotnet/aspnet:6.0

# See list here: https://calibre-ebook.com/download_linux
RUN apt-get update && \
    apt-get install -y xdg-utils wget xz-utils python3 libegl1 libopengl0 && \
    apt-get clean

EXPOSE 80
EXPOSE 443

WORKDIR /app

COPY . .

ENTRYPOINT ["dotnet", "KindlerBot.dll"]