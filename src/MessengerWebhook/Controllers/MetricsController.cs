using MessengerWebhook.Services.Metrics;
using MessengerWebhook.Services.Metrics.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessengerWebhook.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MetricsController : ControllerBase
{
    private readonly IMetricsAggregationService _aggregationService;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(
        IMetricsAggregationService aggregationService,
        ILogger<MetricsController> logger)
    {
        _aggregationService = aggregationService;
        _logger = logger;
    }

    [HttpGet("summary")]
    [ResponseCache(Duration = 300)]
    public async Task<ActionResult<MetricsSummaryDto>> GetSummary(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        var (validatedStart, validatedEnd) = ValidateDateRange(startDate, endDate);

        try
        {
            // Always use authenticated user's tenant - never accept from query params
            var summary = await _aggregationService.GetSummaryAsync(
                validatedStart, validatedEnd, null, cancellationToken);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get metrics summary");
            return StatusCode(500, "Failed to retrieve metrics");
        }
    }

    [HttpGet("variants")]
    [ResponseCache(Duration = 300)]
    public async Task<ActionResult<VariantComparisonDto>> GetVariantComparison(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        var (validatedStart, validatedEnd) = ValidateDateRange(startDate, endDate);

        try
        {
            // Always use authenticated user's tenant - never accept from query params
            var comparison = await _aggregationService.GetVariantComparisonAsync(
                validatedStart, validatedEnd, null, cancellationToken);
            return Ok(comparison);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get variant comparison");
            return StatusCode(500, "Failed to retrieve metrics");
        }
    }

    [HttpGet("pipeline")]
    [ResponseCache(Duration = 300)]
    public async Task<ActionResult<PipelinePerformanceDto>> GetPipelinePerformance(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        var (validatedStart, validatedEnd) = ValidateDateRange(startDate, endDate);

        try
        {
            // Always use authenticated user's tenant - never accept from query params
            var performance = await _aggregationService.GetPipelinePerformanceAsync(
                validatedStart, validatedEnd, null, cancellationToken);
            return Ok(performance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pipeline performance");
            return StatusCode(500, "Failed to retrieve metrics");
        }
    }

    private static (DateTime start, DateTime end) ValidateDateRange(
        DateTime? startDate,
        DateTime? endDate)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-14);
        var end = endDate ?? DateTime.UtcNow;

        // Prevent inverted ranges
        if (start >= end)
        {
            throw new BadHttpRequestException("startDate must be before endDate");
        }

        // Limit to 90 days max to prevent unbounded queries
        if ((end - start).TotalDays > 90)
        {
            throw new BadHttpRequestException("Date range cannot exceed 90 days");
        }

        // Prevent future dates
        if (end > DateTime.UtcNow)
        {
            end = DateTime.UtcNow;
        }

        return (start, end);
    }
}
