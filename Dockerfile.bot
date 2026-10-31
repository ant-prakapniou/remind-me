FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

COPY src/RemindMe.Core/RemindMe.Core.csproj src/RemindMe.Core/
COPY src/RemindMe.Bot/RemindMe.Bot.csproj src/RemindMe.Bot/
RUN dotnet restore src/RemindMe.Bot/RemindMe.Bot.csproj

COPY src/RemindMe.Core/ src/RemindMe.Core/
COPY src/RemindMe.Bot/ src/RemindMe.Bot/
RUN dotnet publish src/RemindMe.Bot/RemindMe.Bot.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:11.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "RemindMe.Bot.dll"]