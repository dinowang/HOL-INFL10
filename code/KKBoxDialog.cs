using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Builder.Dialogs;

public class KKBoxDialog : Dialog
{
    public KKBoxDialog (LuisRecognizer luisRecognizer) : base (nameof (KKBoxDialog))
    {
        LuisRecognizer = luisRecognizer;
    }

    public LuisRecognizer LuisRecognizer { get; }

    public override async Task<DialogTurnResult> BeginDialogAsync (DialogContext dc, 
        object options = null, 
        CancellationToken cancellationToken = default (CancellationToken))
    {
        return await dc.ContinueDialogAsync(cancellationToken);
    }

    public override async Task<DialogTurnResult> ContinueDialogAsync(DialogContext dc, 
        CancellationToken cancellationToken = default(CancellationToken))
    {
        var result = await this.LuisRecognizer.RecognizeAsync (dc.Context, cancellationToken);
        var indent = result.GetTopScoringIntent().intent;
        await dc.Context.SendActivityAsync(MessageFactory.Text($"您的意圖是: {indent}, {result.Entities}"));
        return new DialogTurnResult (DialogTurnStatus.Waiting);
    }
}