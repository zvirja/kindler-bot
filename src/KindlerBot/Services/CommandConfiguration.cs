using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KindlerBot.Commands;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace KindlerBot.Services;

public class CommandConfiguration : IHostedService
{
    private readonly ITelegramBotClient _botClient;
    private readonly ITelegramCommands _commands;
    private readonly ILogger<CommandConfiguration> _logger;

    public CommandConfiguration(ITelegramBotClient botClient, ITelegramCommands commands, ILogger<CommandConfiguration> logger)
    {
        _botClient = botClient;
        _commands = commands;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var botCommands = _commands.AvailableCommands.ToArray();
        await _botClient.SetMyCommandsAsync(botCommands, cancellationToken);
        _logger.LogInformation("Configured {count} commands", botCommands.Length);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}