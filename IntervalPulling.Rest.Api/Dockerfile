FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["IntervalPulling.Rest.Api/IntervalPulling.Rest.Api.csproj", "IntervalPulling.Rest.Api/"]
RUN dotnet restore "IntervalPulling.Rest.Api/IntervalPulling.Rest.Api.csproj"
COPY . .
WORKDIR "/src/IntervalPulling.Rest.Api"
RUN dotnet build "IntervalPulling.Rest.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "IntervalPulling.Rest.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IntervalPulling.Rest.Api.dll"]
