using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace KindlerBot.Security;

public interface IChatAuthorization
{
    ValueTask<bool> IsAuthorized(Update update);
}