using System.Threading;
using System.Threading.Tasks;
using KindlerBot.Commands;
using KindlerBot.Configuration;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KindlerBot.Services;

internal class UpdateNotifier : IHostedService
{
    private readonly IMediator _mediator;
    private readonly IConfigStore _configStore;
    private readonly ILogger<UpdateNotifier> _logger;

    public UpdateNotifier(IMediator mediator, IConfigStore configStore, ILogger<UpdateNotifier> logger)
    {
        _mediator = mediator;
        _configStore = configStore;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var lastVersion = await _configStore.GetLastAppVersion();
        var currentVersion = BotVersion.Current.FileVersion;

        if (currentVersion != lastVersion)
        {
            await _configStore.SetLastAppVersion(currentVersion);
            _ = _mediator.Publish(new UpdateNotification(BotVersion.Current), cancellationToken: default);
            _logger.LogInformation("Sent update notification - different version. Old: {old}, New: {new}", lastVersion, currentVersion);
        }
        else
        {
            _logger.LogInformation("Skipped update notification - same version");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
