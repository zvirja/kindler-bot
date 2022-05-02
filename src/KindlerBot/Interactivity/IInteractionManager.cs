using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace KindlerBot.Interactivity;

public interface IInteractionManager
{
    bool ResumeInteraction(Update update);
    Task<Update> AwaitNextUpdate(ChatId chatId);
}