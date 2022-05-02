using System.Collections.Concurrent;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace KindlerBot.Interactivity;

internal class InteractionManager : IInteractionManager
{
    private readonly ConcurrentDictionary<ChatId, TaskCompletionSource<Update>> _interactions = new();

    public bool ResumeInteraction(Update update)
    {
        var chatId = update.TryGetChatId();
        if (chatId == null)
            return false;

        if (_interactions.TryRemove(chatId, out var outgoingWorkflow))
        {
            outgoingWorkflow.SetResult(update);
            return true;
        }

        return false;
    }

    public Task<Update> AwaitNextUpdate(ChatId chatId)
    {
        if (_interactions.TryGetValue(chatId, out var outgoingWorkflow))
        {
            outgoingWorkflow.SetCanceled();
        }

        var tcs = new TaskCompletionSource<Update>();
        _interactions[chatId] = tcs;

        return tcs.Task;
    }
}