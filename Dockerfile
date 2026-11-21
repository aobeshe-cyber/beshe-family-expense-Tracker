FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Expense Tracker.csproj", "Expense Tracker/"]
COPY ["ExpenseTracker.Web/ExpenseTracker.Web.csproj", "ExpenseTracker.Web/"]
RUN dotnet restore "ExpenseTracker.Web/ExpenseTracker.Web.csproj"

COPY . "Expense Tracker/"
RUN mv "Expense Tracker/ExpenseTracker.Web" "ExpenseTracker.Web"
WORKDIR "/src/ExpenseTracker.Web"
RUN dotnet publish "ExpenseTracker.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ExpenseTracker.Web.dll"]