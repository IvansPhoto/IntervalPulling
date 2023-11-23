FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["IntervalPulling.BlazorClient/IntervalPulling.BlazorClient.csproj", "IntervalPulling.BlazorClient/"]
RUN dotnet restore "IntervalPulling.BlazorClient/IntervalPulling.BlazorClient.csproj"
COPY . .
WORKDIR "/src/IntervalPulling.BlazorClient"
RUN dotnet build "IntervalPulling.BlazorClient.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "IntervalPulling.BlazorClient.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IntervalPulling.BlazorClient.dll"]
