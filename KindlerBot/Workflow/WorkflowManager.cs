using System.Collections.Concurrent;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace KindlerBot.Workflow
{
    internal class WorkflowManager : IWorkflowManager
    {
        private readonly ConcurrentDictionary<ChatId, TaskCompletionSource<Update>> _workflows = new();

        public bool ResumeWorkflow(Update update)
        {
            var chatId = update.TryGetChatId();
            if (chatId == null)
                return false;

            if (_workflows.TryRemove(chatId, out var outgoingWorkflow))
            {
                outgoingWorkflow.SetResult(update);
                return true;
            }

            return false;
        }

        public Task<Update> AwaitNextUpdate(ChatId chatId)
        {
            if (_workflows.TryGetValue(chatId, out var outgoingWorkflow))
            {
                outgoingWorkflow.SetCanceled();
            }

            var tcs = new TaskCompletionSource<Update>();
            _workflows[chatId] = tcs;

            return tcs.Task;
        }
    }
}
