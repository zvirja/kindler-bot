using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace KindlerBot.Workflow
{
    public interface IWorkflowManager
    {
        bool ResumeWorkflow(Update update);
        Task<Update> AwaitNextUpdate(ChatId chatId);
    }
}
