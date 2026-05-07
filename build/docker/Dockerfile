FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG TARGETPLATFORM

# See list here: https://calibre-ebook.com/download_linux
RUN apt-get update && \
    apt-get install -y tini xdg-utils wget xz-utils python3 libegl1 libopengl0 libxcb-cursor0 libdeflate0 libxkbcommon0 && \
    apt-get clean

RUN wget -nv -O- https://download.calibre-ebook.com/linux-installer.sh | sh /dev/stdin install_dir=/opt

EXPOSE 8080

WORKDIR /app

COPY ./${TARGETPLATFORM}/ .

ENTRYPOINT ["/usr/bin/tini", "--", "dotnet", "KindlerBot.dll"]
