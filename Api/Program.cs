using Api;
using Api.Jobs;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Http;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var services = builder.Services;

services.AddControllers();
services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

services.ConfigureAll<HttpClientFactoryOptions>(options =>
{
  options.HttpMessageHandlerBuilderActions.Add(httpMessageHandlerBuilder =>
  {
    httpMessageHandlerBuilder.AdditionalHandlers.Add(httpMessageHandlerBuilder.Services.GetRequiredService<PerformanceRequestHandler>());
  });
});
services.AddScoped<PerformanceRequestHandler>();
services.AddHttpClient();
services.AddSerilog();

services.AddScoped<TestFireAndForgetJob>();
services.AddScoped<TestRecurringJob>();
services.AddScoped<FailedJobsCleanerRecurringJob>();

services.AddHangfire(globalConfiguration =>
{
    globalConfiguration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(
            configuration.GetConnectionString("Default"),
            new SqlServerStorageOptions
            {
                JobExpirationCheckInterval = TimeSpan.FromSeconds(30)
            });
});
services.AddHangfireServer();

var serilogConfiguration = configuration.GetSection("Serilog");
Log.Logger = new LoggerConfiguration()
  .Enrich.FromLogContext()
  .Enrich.WithProcessName()
  .Enrich.WithProcessId()
  .Enrich.WithThreadName()
  .Enrich.WithThreadId()
  .Enrich.WithMemoryUsage()
  .Enrich.WithProperty("ContainerId", Environment.GetEnvironmentVariable("HOSTNAME"))
  .WriteTo.Console()
  .WriteTo.Seq(serilogConfiguration.GetSection("Seq")!.GetValue<string>("Url")!)
  .CreateBootstrapLogger();

try
{
  var app = builder.Build();

  using var scope = app.Services.CreateScope();
  var testRecurringJob = scope.ServiceProvider.GetRequiredService<TestRecurringJob>();
  var failedJobsCleanerRecurringJob = scope.ServiceProvider.GetRequiredService<FailedJobsCleanerRecurringJob>();
  var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
  recurringJobManager.AddOrUpdate(
      app.Configuration.GetValue<string>("TestRecurringJob"),
      () => testRecurringJob.Process(CancellationToken.None),
      Cron.Minutely);

  recurringJobManager.AddOrUpdate(
      app.Configuration.GetValue<string>("FailedJobsCleanerRecurringJob"),
      () => failedJobsCleanerRecurringJob.Process(CancellationToken.None),
      Cron.Minutely);

  app.UseSwagger();
  app.UseSwaggerUI();
  app.UseSerilogRequestLogging();
  app.UseAuthorization();
  app.UseHangfireDashboard();

  app.MapGet(
      "api/fire-and-forget/test",
      (HttpContext httpContext, [FromServices] IBackgroundJobClient backgroundJobClient, [FromServices] TestFireAndForgetJob testFireAndForgetJob) =>
      {
          backgroundJobClient.Enqueue(() => testFireAndForgetJob.Process(CancellationToken.None));
          return Results.Ok();
      });

  app.MapGet(
      "api/fire-and-forget/fail",
      (HttpContext httpContext, [FromServices] IBackgroundJobClient backgroundJobClient, [FromServices] TestFireAndForgetJob testFireAndForgetJob) =>
      {
          backgroundJobClient.Enqueue(() => testFireAndForgetJob.Fail(CancellationToken.None));
          return Results.Ok();
      });

  app.Run();
}
catch(Exception e)
{
  Log.Error(e, "Unexpected error occurred");
}
finally
{
  await Log.CloseAndFlushAsync();
}