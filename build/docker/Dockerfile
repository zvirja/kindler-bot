FROM mcr.microsoft.com/dotnet/aspnet:8.0

# See list here: https://calibre-ebook.com/download_linux
RUN apt-get update && \
    apt-get install -y xdg-utils wget xz-utils python3 libegl1 libopengl0 libxcb-cursor0 libdeflate0 libxkbcommon0 && \
    apt-get clean

RUN wget -nv -O- https://download.calibre-ebook.com/linux-installer.sh | sh /dev/stdin install_dir=/opt

EXPOSE 8080

WORKDIR /app

COPY . .

ENTRYPOINT ["dotnet", "KindlerBot.dll"]
