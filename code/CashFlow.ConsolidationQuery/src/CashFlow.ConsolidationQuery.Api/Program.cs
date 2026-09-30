using CashFlow.ConsolidationQuery.Core;
using CashFlow.ConsolidationQuery.Core.Interfaces;
using CashFlow.ConsolidationQuery.Repository;
using CashFlow.ConsolidationQuery.Repository.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();

var connectionString =
    builder.Configuration.GetConnectionString("PostgreSql")
    ?? throw new InvalidOperationException(
        "Connection string 'PostgreSql' was not configured.");

builder.Services.AddScoped<IConsolidationQueryService, ConsolidationQueryService>();

builder.Services.AddScoped<IConsolidationQueryRepository>(_ =>
    new ConsolidationQueryRepository(connectionString));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();