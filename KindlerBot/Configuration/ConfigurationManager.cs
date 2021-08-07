using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace KindlerBot.Configuration
{
    internal class ConfigurationManager : IConfigurationManager
    {

        public async ValueTask<string?> GetChatEmail(ChatId chatId)
        {
            return null;
        }

        public async ValueTask SetChatEmail(ChatId chatId, string email)
        {
        }
    }
}
