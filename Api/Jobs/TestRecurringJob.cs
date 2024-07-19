namespace Api.Jobs;

public class TestRecurringJob
{
    private readonly ILogger<TestRecurringJob> _logger;

    public TestRecurringJob(ILogger<TestRecurringJob> logger)
    {
        _logger = logger;
    }

    public Task Process(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RecurringJob");
        return Task.CompletedTask;
    }
}
