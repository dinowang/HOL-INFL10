# 前置條件
Lab 前的預先準備環境

1. Visual Studio / Visual Studio Code
2. .NET Core 2.0 / 2.1
3. [Bot Framework Emulator v4 (Preview)](https://github.com/Microsoft/BotFramework-Emulator/releases)
4. Node.js version 8.5 or later
5. 透過 npm 安裝工具  
   `npm i -g msbot chatdown ludown qnamaker luis-apis botdispatch luisgen`

---

# Step 1. 建立一個會回覆你一樣的話的機器人

## 目的
學習如何建立會運作的 chatbot

## 實作目標
建立一個你說什麼話, 他就回你什麼話的討厭鬼

## 步驟
1. 透過 dotnet cli 建立專案  
   ```sh
   dotnet new web
   ```

2. 透過 msbot 建立 .bot file  
   ```sh
   msbot init -n DemoBot -q
   ```
   > * .bot file 的目的除了是程式碼針對 bot 的 configuration 外, 也是botframework emulator 的描述檔
   > * port 會用 5000 是因為 dotnet cli 預設模板使用 5000 的 port, 當然可以修改

3. 新增連接 endpoint 到 bot file  
   ```sh
   msbot connect endpoint -n development -e http://localhost:5000/api/messages
   ```
   > * endpoint 是告訴程式碼, 我的 Microsoft Id, Key 分別是什麼, 同時也是讓 botframework emulator 知道 bot 的 endpoint  
   > * 事實上 init 時有指定 -e 參數時, 也會建立一筆 endpoint, 但 名稱會同檔案名稱

4. 加入 nuget 套件  
   ```sh
   dotnet add package Microsoft.Bot.Builder
   dotnet add package Microsoft.Bot.Builder.Integration.AspNet.Core
   dotnet add package Microsoft.Bot.Configuration
   ```

5. 在專案中增加 DemoBot 類別繼承自 IBot 並且實作 OnTurnAsync 方法
   [DemoBot.cs](code/DemoBot.cs)

6. 建立 `appsettings.json`, 設定 botFileSecret 跟 botFilePath   
   ```json
   {
     "botFileSecret": "",
     "botFilePath": "DemoBot.bot"
   }
   ```

7. 在 Startup.cs 中, 註冊 service, 使用 middleware
   [Statrup.cs](code/Statrup.cs)

8. 執行 ASP.NET Core 應用程式

9. 執行 Bot Framework Emulator v4 (Preview), 驗證對話是否完成

10. 在 Azure 中新增 Bot Service

11. 新增連接 endpoint 到 bot file  
   ```sh
   msbot connect endpoint -n production -e https://xxx.azurewebsites.net/api/messages -a "appId" -p "appKey"
   ```

---

# Step 2. 保留 bot 狀態

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

4. 修改實作上一節中實作的 `DemoBot`, 將 `StateAccessors` 注入
    ```csharp
    private StateAccessors _accessors;
 
    public DemoBot(StateAccessors accessors)
    {
        this._accessors = accessors;
    }
    ```

5. 修改 `DemoBot` 的 `OnTurnAsync` 方法, 讓機器人在接收到訊息時, 取得這是對話中第幾個訊息, 並且 +1 後放回
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

---

# Step 3. 使用 Dialog 做到更彈性的機器人

## 目的
使用 Dialog 並且保留 bot 狀態

## 實作目標
在用戶加進對話時, 詢問用戶的資料, 並存入 `UserState`

## 步驟
1. 新增 package `Microsoft.Bot.Builder.Dialogs`
    ```sh
    dotnet add package Microsoft.Bot.Builder.Dialogs
    ```

2. 建立類別 `UserProfile`, 有基本資料的屬性
    ```csharp
    public class UserProfile
    {
        public string UserName { get; set; }
        public bool Gender { get;set; }
        public int Age { get; set; }
    }
    ```

3. 在上一節的 `StateAccessors` 中增加建構參數 `UserState`, 並且建立屬性 `UserProfileAccessor`
    ```csharp
    using Microsoft.Bot.Builder.Dialogs;
    
            public StateAccessors(ConversationState convState, UserState usrState)
            {
                this.ConversationState = convState;
                this.UserState = usrState;
                this.CounterAccessor = convState.CreateProperty<int>(nameof(CounterAccessor));
                this.UserProfileAccessor = usrState.CreateProperty<UserProfile>(nameof(UserProfileAccessor));
                this.DialogStateAccessor = usrState.CreateProperty<DialogState>(nameof(DialogStateAccessor));
            }
             
            public IStatePropertyAccessor<UserProfile> UserProfileAccessor { get; set; }
            public IStatePropertyAccessor<DialogState> DialogStateAccessor { get; set; }
            public UserState UserState { get; set; }
    ```

4. 在 `StartUp` 註冊 `UserState`, 因此範例中, 需要儲存使用者資訊到 `UserState` 中
    ```csharp
         public void ConfigureServices (IServiceCollection services) 
         {
             // 省略
             services.AddSingleton<UserState>();
         }
    ```

5. 建立類別 `WelcomeDialog`, 並且實作  
   [WelcomeDialog.cs](3-WelcomeDialog.md)

6. 修改 `DemoBot` 在加入對話時, 先進入 `WelcomeDialog`, 首先設定 Dialog 進入 DialogSet, 並在 `conversationUpdate` 事件時, 開始 Dialog, 並且在 `message` 事件中若對話未結束就繼續對話
   [DialogSet.cs](3-DialogSet.md)

---

# Step 4. 說好的 AI 在哪裡? LUIS
   
## 目的
學習如何整合 LUIS 服務進 bot

## 實作目標
使用 LUIS (Language Understanding Intelligent Service) 了解使用者說話的意圖, 並且給予對應的結果

## 步驟
1. 下載 `kkbox.luis.json` 到目錄 `CognitiveModels` 中
   
2. 使用 Microsoft Account 進入 https://luis.ai

3. 將 `kkbox.luis.json` 匯入成一個新的 APP, 並且發行 (Publish)

4. 在 Manage \ Application Information 中, 複製 Application ID 以及Keys and Endpoints 中的Athoring Key

5. 加入nuget套件 `Microsoft.Bot.Builder.AI.Luis` 
    ```sh
    dotnet add package Microsoft.Bot.Builder.AI.Luis
    ```

6. 在 bot 中加入 luis
    ```sh
    msbot connect luis -n KKBOX -a <appId> --authoringKey <authoringKey> --version 0.1
    ```

7. 實作 `KKBoxDialog`  
   [KKBoxDialog](code/KKBoxdialog.cs)
   
    > 都回傳狀態為 `DialogTurnStatus.Waiting` 的原因是, 讓 Bot 維持在 `KKBoxDialog` 中

8. 在 `Startup.cs` 中, 驗證 `BotConfiguration` 是否有 LuisService, 並且註冊Service
    ```csharp
    using Microsoft.Bot.Builder.AI.Luis;

    public void ConfigureServices (IServiceCollection services)
    {
        // 省略
        var luisService = botConfig.Services.OfType<LuisService>().FirstOrDefault();

        if (luisService == null)
        {
            throw new InvalidOperationException ($"The .bot file does not contain an luis service with name '{environment}'.");
        }
        var luisApp = new LuisApplication(luisService.AppId, luisService.AuthoringKey, luisService.GetEndpoint());
        var recognizer = new LuisRecognizer(luisApp);

        services.AddSingleton<LuisRecognizer>(recognizer);
        services.AddSingleton<KKBoxDialog>();
    }
    ```

9. 修改 `DemoBot.cs`, 注入 `KKBoxDialog` 並且將 `KKBoxDialog` 加入 `DialogSet`
    ```csharp
    public DemoBot (StateAccessors accessors, 
        WelcomeDialog welcomeDialog, 
        KKBoxDialog kkBoxDialog)
    {
        this._accessors = accessors;
        Dialogs = new DialogSet (accessors.DialogStateAccessor);
        this.Dialogs.Add (welcomeDialog)
            .Add (kkBoxDialog);
    }
    ```

10. 修改 `DemoBot.cs`, 將原本 `else if (dialogResult.Status == DialogTurnStatus.Empty)` 的內容刪除, 並改成啟動 `KKBoxDialog`
    ```csharp
    var dialogResult = await dc.ContinueDialogAsync();
    if (dialogResult.Status == DialogTurnStatus.Complete)
    {
        await dc.EndDialogAsync();
        await turnContext.SendActivityAsync(MessageFactory.Text("您可以開始跟我聊天"));
    }
    else if (dialogResult.Status == DialogTurnStatus.Empty)
    {
        await dc.BeginDialogAsync (nameof (KKBoxDialog));
    }
    ```

11. 執行起來後, 確認 bot 正常

12. 因為 `LuisRecognizer.RecognizeAsync` 的結果中, Entity 屬性是 `JObject` 型別, 還是希望用強型別處理
    
    1. 透過 `luisgen` 將意義產生成 .cs 
        ```sh
        luisgen "CognitiveModels\kkbox.luis.json" -cs <namespace>.KKBoxRecognizerConvert
        ```

    2. 修改 `KKBoxDialog` 的 `ContinueDialogAsyn` 方法
        ```csharp
        public override async Task<DialogTurnResult> ContinueDialogAsync(DialogContext dc, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await this.LuisRecognizer.RecognizeAsync<KKBoxRecognizerConvert>(dc.Context, cancellationToken);
            var indent = result.TopIntent().intent;
            await dc.Context.SendActivityAsync(MessageFactory.Text($"您的意圖是: {indent}, {result.Entities}"));
            return new DialogTurnResult (DialogTurnStatus.Waiting);
        }
        ```

13. 加入 `KKBOX.OpenAPI.Standard` 套件
    ```sh
    dotnet add package KKBOX.OpenAPI.Standard
    ```

14. 在 `StartUp` 中註冊 `KKBOXAPI`
    ```csharp
    services.AddSingleton<KKBOX.OpenAPI.KKBOXAPI>(sp =>
    {
        var openApi = new KKBOX.OpenAPI.KKBOXAPI();
        var clientId = "f0ae5ebb40eae042dc819852d30ea96c";
        var clientSecret = "86424de7030ffe62ae52d9d4769aa6f0";
        var authResult = KKBOX.OpenAPI.KKBOXOAuth.SignInAsync(clientId, clientSecret).Result;
        string accessToken = authResult.Content.AccessToken;
        openApi.AccessToken = accessToken;

        return openApi;
    });
    ```

15. 修改 `KKBoxDialog` , 判斷使用者意圖, 根據使用者意圖決定動作
    
    1. 建構子增加 `KKBOXAPI` 參數, 並且放到屬性中
        ```csharp
        public KKBoxDialog (LuisRecognizer luisRecognizer, KKBOXAPI api) : base (nameof (KKBoxDialog))
        {
            LuisRecognizer = luisRecognizer;
            Api = api;
        }

        public KKBOXAPI Api { get; }
        ```

    2. 增加 `GetSearchResultAsync` 跟 `GetChartPlayListAsync` 兩個方法, 可以用來搜尋跟取得熱門榜資訊
        ```csharp
        private async Task<IActivity> GetSearchResultAsync (KKBoxRecognizerConvert result)
        {
            var queryResult = await this.Api.SearchAsync (result.Entities.keyword[0]);
            var attachments = queryResult.Content.Albums.Data
                .Select (p =>
                    new ThumbnailCard (p.Name,
                        p.ReleaseDate,
                        images : p.Images.Select (img => new CardImage (img.Url)).ToList (),
                        tap: new CardAction("openUrl", value: GetKKBoxPlayListUrl(p.Id))).ToAttachment ());
            var activity = MessageFactory.Carousel (attachments);
            return activity;
        }

        private async Task<IActivity> GetChartPlayListAsync (KKBoxRecognizerConvert result)
        {
            var keyword = string.Empty;

            if (result.Entities.artist.Any ())
            {
                keyword = result.Entities.artist[0];
            }
            else if (result.Entities.keyword.Any ())
            {
                keyword = result.Entities.keyword[0];
            }
            var queryResult = await this.Api.SearchAsync (keyword);
            var attachments = queryResult.Content.Albums.Data
                .Select (p =>
                    new ThumbnailCard (p.Name,
                        p.ReleaseDate,
                        images : p.Images.Select (img => new CardImage (img.Url)).ToList (),
                        tap: new CardAction("openUrl", value : GetKKBoxPlayListUrl (p.Id))).ToAttachment ());
            var activity = MessageFactory.Carousel (attachments);
            return activity;
        }

        private string GetKKBoxPlayListUrl (string id) => $"kkbox://playlist/{id}";
        ```
        
    3. 修改方法 `ContinueDialogAsync` 根據意圖執行上步驟增加的兩個方法
        ```csharp
        public override async Task<DialogTurnResult> ContinueDialogAsync (DialogContext dc,
            CancellationToken cancellationToken = default (CancellationToken))
        {
            var result = await this.LuisRecognizer.RecognizeAsync<KKBoxRecognizerConvert> (dc.Context, cancellationToken);
            var indent = result.TopIntent ().intent;
            IActivity activity = null;

            switch (indent)
            {
                case KKBoxRecognizerConvert.Intent.chart:
                    activity = await this.GetChartPlayListAsync (result);
                    break;
                case KKBoxRecognizerConvert.Intent.search:
                    activity = await this.GetSearchResultAsync (result);
                    break;
                default:
                    break;
            }

            if (activity != null)
            {
                await dc.Context.SendActivityAsync(activity);
            }

            return new DialogTurnResult (DialogTurnStatus.Waiting);
        }
        ```
