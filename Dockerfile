FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY PitchPulse.Api/PitchPulse.Api.csproj PitchPulse.Api/
RUN dotnet restore PitchPulse.Api/PitchPulse.Api.csproj
COPY PitchPulse.Api/ PitchPulse.Api/
RUN dotnet publish PitchPulse.Api/PitchPulse.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000
USER $APP_UID
CMD ["dotnet", "PitchPulse.Api.dll"]
