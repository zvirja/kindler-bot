using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using KindlerBot.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KindlerBot.Services;

internal class ChatApprovalCleanup : IHostedService
{
    private readonly IChatApprovalRequestsStore _chatApprovalRequestsStore;
    private readonly ILogger<ChatApprovalCleanup> _logger;
    private Timer? Timer { get; set; }

    public ChatApprovalCleanup(IChatApprovalRequestsStore chatApprovalRequestsStore, ILogger<ChatApprovalCleanup> logger)
    {
        _chatApprovalRequestsStore = chatApprovalRequestsStore;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Timer = new Timer(
            _ =>
            {
                Task.Run(async () => await _chatApprovalRequestsStore.CleanObsoleteRequests(expiration: TimeSpan.FromDays(7)));
                _logger.LogInformation("Run cleanup of old chat approval requests");
            },
            state: null,
            dueTime: TimeSpan.FromDays(1),
            period: TimeSpan.FromDays(1));

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Timer != null)
        {
            await Timer.DisposeAsync();
            Timer = null;
        }
    }
}
