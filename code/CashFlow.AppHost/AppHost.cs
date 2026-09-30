var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.CashFlow_Entry_Api>("entry-api");

builder.Build().Run();