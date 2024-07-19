using Hangfire;

namespace Api.Jobs;

public class TestFireAndForgetJob
{
    private readonly ILogger<TestFireAndForgetJob> _logger;

    public TestFireAndForgetJob(ILogger<TestFireAndForgetJob> logger)
    {
        _logger = logger;
    }

    public Task Process(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fire and Forget");
        return Task.CompletedTask;
    }

    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public Task Fail(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
