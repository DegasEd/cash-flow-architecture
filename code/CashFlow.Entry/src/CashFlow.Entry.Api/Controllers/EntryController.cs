using CashFlow.Entry.Core.Interfaces;
using CashFlow.Entry.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Entry.Api.Controllers;

[ApiController]
[Route("entries")]
public class EntryController : ControllerBase
{
    private readonly IEntryService _entryService;

    public EntryController(IEntryService entryService)
    {
        _entryService = entryService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateEntryRequest request,
        CancellationToken cancellationToken)
    {
        var entry = await _entryService.CreateAsync(
            request,
            cancellationToken);

        return Created(
            $"/entries/{entry.Id}",
            new
            {
                entry.Id,
                entry.AmountInCents,
                Type = entry.Type.ToString(),
                entry.OccurredAt,
                entry.CreatedAt
            });
    }
}