using System.Threading;
using System.Threading.Tasks;
using KindlerBot.Commands;
using KindlerBot.Configuration;
using MediatR;
using Microsoft.Extensions.Hosting;

namespace KindlerBot.Services
{
    internal class UpdateNotifier : IHostedService
    {
        private readonly IMediator _mediator;
        private readonly IConfigStore _configStore;

        public UpdateNotifier(IMediator mediator, IConfigStore configStore)
        {
            _mediator = mediator;
            _configStore = configStore;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var lastVersion = await _configStore.GetLastAppVersion();
            var currentVersion = BotVersion.Current.AppVersion;

            if (currentVersion != lastVersion)
            {
                await _configStore.SetLastAppVersion(currentVersion);
                _ = _mediator.Publish(new UpdateNotification(BotVersion.Current), cancellationToken: default);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
