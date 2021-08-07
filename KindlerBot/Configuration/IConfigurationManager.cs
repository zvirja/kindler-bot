using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace KindlerBot.Configuration
{
    internal interface IConfigurationManager
    {
        public ValueTask SetChatEmail(ChatId chatId, string email);

        public ValueTask<string?> GetChatEmail(ChatId chatId);
    }
}
