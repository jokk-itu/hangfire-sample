using Hangfire;

namespace Api.Jobs;

public class FailedJobsCleanerRecurringJob
{
    private readonly ILogger<FailedJobsCleanerRecurringJob> _logger;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public FailedJobsCleanerRecurringJob(
        ILogger<FailedJobsCleanerRecurringJob> logger,
        IBackgroundJobClient backgroundJobClient)
    {
        _logger = logger;
        _backgroundJobClient = backgroundJobClient;
    }

    public Task Process(CancellationToken cancellationToken)
    {
        var jobApi = JobStorage.Current.GetMonitoringApi();
        var failedJobs = jobApi.FailedJobs(0, 1000);

        while (failedJobs.Count > 0)
        {
            foreach (var (jobId, job) in failedJobs)
            {
                _backgroundJobClient.Delete(jobId);
            }

            failedJobs = jobApi.FailedJobs(0, 1000);
        }

        return Task.CompletedTask;
    }
}
