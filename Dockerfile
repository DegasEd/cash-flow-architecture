# ============================================================
# Cash Flow Architecture
# Single multi-target Dockerfile
#
# Targets:
#   entry
#   outbox
#   consolidation-processor
#   consolidation-query
# ============================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-base
WORKDIR /src
COPY code/ code/

# ============================================================
# Entry API
# ============================================================

FROM build-base AS build-entry
RUN dotnet restore \
    code/CashFlow.Entry/src/CashFlow.Entry.Api/CashFlow.Entry.Api.csproj

RUN dotnet publish \
    code/CashFlow.Entry/src/CashFlow.Entry.Api/CashFlow.Entry.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS entry
WORKDIR /app
COPY --from=build-entry /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CashFlow.Entry.Api.dll"]

# ============================================================
# Outbox Worker
# ============================================================

FROM build-base AS build-outbox
RUN dotnet restore \
    code/CashFlow.Outbox/src/CashFlow.Outbox.Worker/CashFlow.Outbox.Worker.csproj

RUN dotnet publish \
    code/CashFlow.Outbox/src/CashFlow.Outbox.Worker/CashFlow.Outbox.Worker.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS outbox
WORKDIR /app
COPY --from=build-outbox /app/publish .
ENTRYPOINT ["dotnet", "CashFlow.Outbox.Worker.dll"]

# ============================================================
# Consolidation Processor
# ============================================================

FROM build-base AS build-consolidation-processor
RUN dotnet restore \
    code/CashFlow.ConsolidationProcessor/src/CashFlow.ConsolidationProcessor.Worker/CashFlow.ConsolidationProcessor.Worker.csproj

RUN dotnet publish \
    code/CashFlow.ConsolidationProcessor/src/CashFlow.ConsolidationProcessor.Worker/CashFlow.ConsolidationProcessor.Worker.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS consolidation-processor
WORKDIR /app
COPY --from=build-consolidation-processor /app/publish .
ENTRYPOINT ["dotnet", "CashFlow.ConsolidationProcessor.Worker.dll"]

# ============================================================
# Consolidation Query API
# ============================================================

FROM build-base AS build-consolidation-query
RUN dotnet restore \
    code/CashFlow.ConsolidationQuery/src/CashFlow.ConsolidationQuery.Api/CashFlow.ConsolidationQuery.Api.csproj

RUN dotnet publish \
    code/CashFlow.ConsolidationQuery/src/CashFlow.ConsolidationQuery.Api/CashFlow.ConsolidationQuery.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS consolidation-query
WORKDIR /app
COPY --from=build-consolidation-query /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CashFlow.ConsolidationQuery.Api.dll"]