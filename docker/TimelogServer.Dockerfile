#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Runners/Joyn.Timelog.Server/Joyn.Timelog.Server.csproj", "Runners/Joyn.Timelog.Server/"]
COPY ["Modules/Joyn.Timelog.Common/Joyn.Timelog.Common.csproj", "Modules/Joyn.Timelog.Common/"]
RUN dotnet restore "./Runners/Joyn.Timelog.Server/./Joyn.Timelog.Server.csproj"
COPY . .
WORKDIR "/src/Runners/Joyn.Timelog.Server"
RUN dotnet build "./Joyn.Timelog.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Joyn.Timelog.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN mkdir /logs
RUN chmod 777 /logs

ENTRYPOINT ["dotnet", "Joyn.Timelog.Server.dll"]
