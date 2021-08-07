using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace KindlerBot.Configuration
{
    internal interface IConfigStore
    {
        public ValueTask SetChatEmail(ChatId chatId, string email);

        public ValueTask<string?> GetChatEmail(ChatId chatId);
    }
}
