using CashFlow.ConsolidationQuery.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.ConsolidationQuery.Api.Controllers;

[ApiController]
[Route("consolidations")]
public class ConsolidationController : ControllerBase
{
    private readonly IConsolidationQueryService _service;

    public ConsolidationController(
        IConsolidationQueryService service)
    {
        _service = service;
    }

    [HttpGet("{date}")]
    public async Task<IActionResult> GetByDateAsync(
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var consolidation =
            await _service.GetByDateAsync(
                date,
                cancellationToken);

        if (consolidation is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            consolidation.Date,
            consolidation.BalanceInCents,
            consolidation.UpdatedAt
        });
    }
}