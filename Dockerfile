FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["lib/Crovus/Crovus.csproj", "lib/Crovus/"]
COPY ["src/SadBot/SadBot.csproj", "src/SadBot/"]
RUN dotnet restore "src/SadBot/SadBot.csproj"
COPY lib/Crovus/ lib/Crovus/
COPY src/SadBot/ src/SadBot/
RUN dotnet publish "src/SadBot/SadBot.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 CMD ["dotnet", "SadBot.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "SadBot.dll"]
