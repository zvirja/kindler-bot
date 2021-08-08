#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443
RUN apt-get update && \
    apt-get install -y xdg-utils && \
    apt-get clean

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["KindlerBot/KindlerBot.csproj", "KindlerBot/"]
RUN dotnet restore "KindlerBot/KindlerBot.csproj"
COPY . .
WORKDIR "/src/KindlerBot"
RUN dotnet build "KindlerBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "KindlerBot.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "KindlerBot.dll"]