1. 修改 `Startup`, 註冊 `WelcomeDialog`
   
    ```csharp
    public void ConfigureServices (IServiceCollection services)
    {
        services.AddSingleton<WelcomeDialog>();
    }
    ```

2. 在 `DemoBot.cs` 中建立 `DialogSet`, 注入並且加入剛剛建立的 `WelcomeDialog`

    ```csharp
    using Microsoft.Bot.Builder.Dialogs;
    
    public DemoBot(StateAccessors accessors, WelcomeDialog welcomeDialog)
    {
        // 省略
        Dialogs = new DialogSet(accessors.DialogStateAccessor);
        Dialogs.Add(welcomeDialog);
    }

    private DialogSet Dialogs { get; set; }
    ```

3. 在 `DemoBot.cs` 中修改 `OnTurnAsync` 方法, 建立 `DialogContext`

    ```csharp
    public async Task OnTurnAsync (ITurnContext turnContext, CancellationToken cancellationToken = default (CancellationToken))
    {
        var dc = await Dialogs.CreateContextAsync(turnContext);
        // todo: 判斷activity 的狀態, 做出對應動作, 下一步驟補齊
    }

4. 延續上步驟的程式碼, 在 `dialogResult` 之後, 在 `ConversationUpdate` 事件時, 將 `DialogSet` 開始 Dialog

    ```csharp
    var activity = turnContext.Activity;

    if (activity.Type == ActivityTypes.Message) 
    {
        // todo: 延續上次的dialog, 取得 dialog 的result, 做出對應動作, 下一步驟補齊
    }
    else if (activity.Type == ActivityTypes.ConversationUpdate)
    {
        if (activity.MembersAdded.Any())
        {
            foreach (var member in activity.MembersAdded)
            {
                if (member.Id != activity.Recipient.Id)
                {
                    await dc.BeginDialogAsync(nameof(WelcomeDialog));
                }
            }
        }
    }
    ```

5. 延續上步驟程式碼, 在 `Message` 事件時, 繼續 dialog, 並取得對話結果, 若是完成, 就將對話結束, 若對話狀態是 Empty (表示沒有對話在執行), 則照上一案例回復

    ```csharp
    var dialogResult = await dc.ContinueDialogAsync();
    if (dialogResult.Status == DialogTurnStatus.Complete)
    {
        await dc.EndDialogAsync();
        await turnContext.SendActivityAsync(MessageFactory.Text("您可以開始跟我聊天"));
    }
    else if (dialogResult.Status == DialogTurnStatus.Empty)
    {
        var count = await this._accessors.CounterAccessor.GetAsync(turnContext, () => default(int), cancellationToken);
        var reply = activity.CreateReply($"您說了: {activity.Text}, 這是您第 {++count} 次留言");
        await turnContext.SendActivityAsync(reply);
        await this._accessors.CounterAccessor.SetAsync(turnContext, count, cancellationToken);
    }
    ```

6. 在 `OnTurnAsync` 的最後補上儲存 `UserState` 跟 `ConversationState` 的動作, 讓 `UserProfile`、`DialogState` 和 Counter 可以被儲存

    ```csharp
    await this._accessors.ConversationState.SaveChangesAsync(turnContext);
    await this._accessors.UserState.SaveChangesAsync(turnContext);
    ```
