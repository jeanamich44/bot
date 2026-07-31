FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ChezRheyyBot.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
EXPOSE 8080
RUN apt-get update && apt-get install -y libgdiplus fontconfig && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "ChezRheyyBot.dll"]
