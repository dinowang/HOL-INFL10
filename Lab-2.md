# Lab-2. 保留 bot 狀態

## 目的
學習如何將聊天狀態保留

## 名詞
* `UserState`: 使用者狀態
* `ConversationState`: 對話狀態, 將是所有在同一對話中的人共用
* `PrivateConversationState`: 對話狀態, 但每個成員都是獨立

## 實作目標
保留這句話是這次的對話中的第幾句話

## 步驟
1. 建立類別 `StateAccessors` 用來儲存狀態的存取器, 建構參數為 `ConversationState`, 並且建立屬性 `CounterAccessor`
    ```csharp
    using Microsoft.Bot.Builder;
    
    public class StateAccessors
    {
        public StateAccessors(ConversationState convState)
        {
            this.ConversationState = convState;
            this.CounterAccessor = convState.CreateProperty<int>(nameof(CounterAccessor));
        }
 
        public IStatePropertyAccessor<int> CounterAccessor { get; set; }
 
        public ConversationState ConversationState { get; }
    }
    ```

2. 狀態的儲存會透過繼承自 `IStorage` 的類別, 在 `Startup.cs` 中, 註冊每當遇到 `IStorage` 時, 使用 `MemoryStorage` 注入. 並且註冊 `ConversationState`
    ```csharp
    using Microsoft.Bot.Builder;
 
    // 省略
         public void ConfigureServices (IServiceCollection services) 
         {
             // 省略
             services.AddSingleton<IStorage, MemoryStorage>()
                 .AddSingleton<ConversationState>();
         }
    ```

    > 這邊使用 MemoryStorage, 但在 web apps 中建議用別的儲存機制 (ex: Microsoft.Bot.Builder.Azure.AzureBlobStorage) 達成持久狀態儲存  
    > AddSingleton: 一旦實例化就不會回收, 運行期間都用同一個  
    > AddTransient: 每次注入都會產生一個實例  
    > AddScoped: 每次的請求都會只產生一個實例, 不同次的Request都會用不同的

3. 在 `Startup.cs` 中註冊 `StateAccessors`
    ```csharp
    public void ConfigureServices (IServiceCollection services) 
    {
        // 省略
        services.AddSingleton<StateAccessors>();
    }
    ```

4. 修改實作上一節中實作的 `DemoBot.cs`, 將 `StateAccessors` 注入
    ```csharp
    private StateAccessors _accessors;
 
    public DemoBot(StateAccessors accessors)
    {
        this._accessors = accessors;
    }
    ```

5. 修改 `DemoBot.cs` 的 `OnTurnAsync` 方法, 讓機器人在接收到訊息時, 取得這是對話中第幾個訊息, 並且 +1 後放回
    ```csharp
    public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
    {
        var activity = turnContext.Activity;
        if (activity.Type == ActivityTypes.Message)
        {
            var count = await this._accessors.CounterAccessor
                .GetAsync(turnContext, () => default(int), cancellationToken); 
            var reply = activity.CreateReply($"您說了: {activity.Text}, 這是您第 {++count} 次留言");
            await turnContext.SendActivityAsync(reply);
            await this._accessors.CounterAccessor.SetAsync(turnContext, count, cancellationToken);
        }
        await this._accessors.ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
    }
    ```
