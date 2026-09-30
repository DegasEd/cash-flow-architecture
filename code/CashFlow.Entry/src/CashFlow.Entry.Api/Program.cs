using CashFlow.Entry.Core.Interfaces;
using CashFlow.Entry.Core.Services;
using CashFlow.Entry.Repository.Interfaces;
using CashFlow.Entry.Repository.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();
builder.Services.AddControllers();

var connectionString =
    builder.Configuration.GetConnectionString("PostgreSql")
    ?? throw new InvalidOperationException(
        "Connection string 'PostgreSql' was not configured.");

builder.Services.AddScoped<IEntryService, EntryService>();

builder.Services.AddScoped<IEntryRepository>(_ =>
    new EntryRepository(connectionString));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();