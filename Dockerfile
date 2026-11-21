# Stage 1: Build .NET 10 Web App (which references Expense Tracker)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore dependencies (update .csproj names if yours differ)
COPY ["Expense Tracker/Expense Tracker.csproj", "Expense Tracker/"]
COPY ["ExpenseTracker.Web/ExpenseTracker.Web.csproj", "ExpenseTracker.Web/"]
RUN dotnet restore "ExpenseTracker.Web/ExpenseTracker.Web.csproj"

# Copy everything else and publish in Release mode
COPY . .
WORKDIR "/src/ExpenseTracker.Web"
RUN dotnet publish "ExpenseTracker.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Listen on port 8080 inside the container
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ExpenseTracker.Web.dll"]