FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["QuizGamePlatform.csproj", "./"]
RUN dotnet restore "./QuizGamePlatform.csproj"

# Copy everything else and build the release
COPY . .
RUN dotnet publish "QuizGamePlatform.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Process the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose port (ASP.NET Core uses 8080 by default since .NET 8+)
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "QuizGamePlatform.dll"]
