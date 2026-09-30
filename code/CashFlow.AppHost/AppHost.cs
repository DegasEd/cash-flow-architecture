var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.CashFlow_Entry_Api>("entry-api");

builder.AddProject<Projects.CashFlow_Outbox_Worker>("outbox-worker");

builder.AddProject<Projects.CashFlow_ConsolidationProcessor_Worker>(
    "consolidation-processor");

builder.AddProject<Projects.CashFlow_ConsolidationQuery_Api>(
    "consolidation-query-api");

builder.Build().Run();