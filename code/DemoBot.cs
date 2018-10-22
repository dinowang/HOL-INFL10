using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

public class DemoBot : IBot
{
    public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
    {
        var activity = turnContext.Activity;
        if (activity.Type == ActivityTypes.Message)
        {
            var reply = activity.CreateReply($"您說了: {activity.Text}");
            await turnContext.SendActivityAsync(reply);
        }
    }
}