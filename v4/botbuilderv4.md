# Required
1. visual studio / visual studio code
2. dotnet core 2.0 / 2.1
3. bot framework emulator v4 (preview) [下載](https://github.com/Microsoft/BotFramework-Emulator/releases)
4. node.js version 8.5 or later
5. 透過 npm 安裝工具  
`npm i -g msbot chatdown ludown qnamaker luis-apis botdispatch luisgen`

# 1 - 建立一個會回覆你一樣的話的機器人
## 目的
學習如何建立會運作的chatbot
## 實作目標
建立一個你說什麼話, 他就回你什麼話的討厭鬼
## 步驟
1.  透過 dotnet cli 建立專案  
    ```sh
    dotnet new web
    ```

2.  透過msbot 建立 .bot file  
    ```sh
    msbot init -n DemoBot -q
    ```
    > * .bot file 的目的除了是程式碼針對 bot 的configuration外, 也是botframework emulator 的描述檔
    > * port 會用 5000 是因為dotnet cli 預設模板使用5000的port, 當然可以修改
3.  新增連接 endpoint 到 bot file  
    ```
    msbot connect endpoint -n development -e http://localhost:5000/api/messages
    msbot connect endpoint -n production -e https://xxx.azurewebsites.net/api/messages -a "appId" -p "appKey"
    ```
    > * endpoint 是告訴程式碼, 我的 Microsoft Id, Key分別是什麼, 同時也是讓 botframework emulator 知道bot的endpoint  
    > * 事實上 init 時有指定 -e 參數時, 也會建立一筆endpoint, 但 名稱會同檔案名稱
4.  加入 nuget 套件  
    ```sh
    dotnet add package Microsoft.Bot.Builder
    dotnet add package Microsoft.Bot.Builder.Integration.AspNet.Core
    dotnet add package Microsoft.Bot.Configuration
    ```

5.  在專案中增加類別繼承自 IBot 並且實作OnTurnAsync方法
    ```csharp
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
                var reply = activity.CreateReply($"您說了:  {activity.Text}");
                await turnContext.SendActivityAsync(reply);
            }
        }
    }
    ```

6.  建立 `appsettings.json`, 設定 botFileSecret 跟 botFilePath
    ```json
    {
      "botFileSecret": "",
      "botFilePath": "DemoBot.bot"
    }
    ```

7.  在startup.cs中, 註冊service, 使用middleware
    ```csharp
    using Microsoft.Bot.Builder.Integration.AspNet.Core;
    using Microsoft.Bot.Configuration;
    using Microsoft.Bot.Connector.Authentication;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    // 忽略中間
        private bool _isProduction;
        private ILoggerFactory _loggerFactory;

        public Startup (IConfiguration configuration) 
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices (IServiceCollection services) 
        {
            var secretKey = Configuration.GetSection ("botFileSecret")?.Value;
            var botFilePath = Configuration.GetSection ("botFilePath")?.Value;

            var botConfig = BotConfiguration.Load (botFilePath ?? "./BotFramework.bot", secretKey);

            services.AddSingleton (sp => botConfig ??
                throw new InvalidOperationException ($"The .bot config file could not be loaded. ({botConfig})"));

            var environment = _isProduction ? "production" : "development";
            var service = botConfig.Services.Where (s => s.Type == "endpoint" && s.Name == environment).FirstOrDefault ();

            if (!(service is EndpointService endpointService)) 
            {
                throw new InvalidOperationException ($"The .bot file does not contain an endpoint with name '{environment}'.");
            }

            services.AddBot<DemoBot> (options => 
            {

                options.CredentialProvider = new SimpleCredentialProvider (endpointService.AppId, endpointService.AppPassword);

                ILogger logger = _loggerFactory.CreateLogger<DemoBot> ();

                options.OnTurnError = async (context, exception) => 
                {
                    logger.LogError ($"Exception caught : {exception}");
                    await context.SendActivityAsync ("Sorry, it looks like something went wrong.");
                };
            });
        }

        public void Configure (IApplicationBuilder app, 
            IHostingEnvironment env, 
            ILoggerFactory loggerFactory) 
        {

            this._isProduction = env.IsProduction ();
            this._loggerFactory = loggerFactory;

            if (env.IsDevelopment ()) {
                app.UseDeveloperExceptionPage ();
            }

            app.UseDefaultFiles ()
                .UseStaticFiles ()
                .UseBotFramework ();
        }
    // 省略

    ```

# 2 - 保留bot狀態
## 目的
學習如何將聊天狀態保留
## 名詞
* `UserState`: 使用者狀態
* `ConversationState`: 對話狀態, 將是所有在同一對話中的人共用
* `PrivateConversationState`: 對話狀態, 但每個成員都是獨立
## 實作目標
保留這句話是這次的對話中第幾句話
## 步驟
1.  建立類別 `StateAccessors` 用來儲存狀態的存取器, 建構參數為 `ConversationState`, 並且建立屬性 `CounterAccessor`
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
2.  狀態的儲存會透過繼承自 `IStorage` 的類別, 在 `startup.cs` 中, 註冊每當遇到 `IStorage` 時, 使用 `MemoryStorage` 注入. 並且註冊 `ConversationState`
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
    > 這邊使用MemoryStorage, 但在web apps 中建議用別的 (ex: Microsoft.Bot.Builder.Azure.AzureBlobStorage) 達到去狀態  
    > AddSingleton: 一旦實例化就不會回收, 運行期間都用同一個  
    > AddTransient: 每次注入都會產生一個實例
    > AddScoped: 每次的請求都會只產生一個實例, 不同次的Request都會用不同的
 
3.  在 `StartUp.cs` 中, 註冊 `StateAccessors`
    ```csharp
    public void ConfigureServices (IServiceCollection services) 
    {
        // 省略
        services.AddSingleton<StateAccessors>();
    }
    ```
4.  修改實作上一節中實作的 `DemoBot`, 將 `StateAccessors` 注入
    ```csharp
    private StateAccessors _accessors;
 
    public DemoBot(StateAccessors accessors)
    {
        this._accessors = accessors;
    }
    ```
 
5.  修改 `DemoBot` 的 `OnTurnAsync`方法, 讓機器人在接收到訊息時, 取得這是對話中第幾個訊息, 並且+1後放回
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

# 3 - 使用Dialog做到更彈性的機器人
## 目的
使用 Dialog 並且保留 bot 狀態

## 實作目標
在用戶加進對話時, 詢問用戶的資料, 並存入 `UserState`

## 步驟
1.  新增 package `Microsoft.Bot.Builder.Dialogs`
    ```sh
    dotnet add package Microsoft.Bot.Builder.Dialogs
    ```
2.  建立類別 `UserProfile`, 有基本資料的屬性
    ```csharp
    public class UserProfile
    {
        public string UserName { get; set; }
        public bool Gender { get;set; }
        public int Age { get; set; }
    }
    ```
3.  在上一節的 `StateAccessors` 中增加建構參數 `UserState`, 並且建立屬性 `UserProfileAccessor`
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
4.  在 `StartUp` 註冊 `UserState`, 因此範例中, 需要儲存使用者資訊到 `UserState` 中
    ```csharp
         public void ConfigureServices (IServiceCollection services) 
         {
             // 省略
             services.AddSingleton<UserState>();
         }
    ```
5.  建立類別 `WelcomeDialog`, 並且實作
    1.  建立類別 `WelcomeDialog`, 繼承自 `Microsoft.Bot.Builder.Dialogs.ComponentDialog`, 需要實作一個參數的建構子, 參數為Dialog 名稱
        ```csharp  
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using System.Threading;
        using System;
        using Microsoft.Bot.Builder.Dialogs.Choices;
        using Microsoft.Bot.Builder.Dialogs;
        using Microsoft.Bot.Builder;

        public class WelcomeDialog : ComponentDialog
        {
            public WelcomeDialog() : base(nameof(WelcomeDialog)) {}
        }
        ```
    2.  會用到 `StateAccessors`, 所以在建構子加入型別為 StateAccessors 的參數, 並且寫入屬性
        ```csharp
        public WelcomeDialog(StateAccessors accessors) : base(nameof(WelcomeDialog))
        {
            this.StateAccessors = accessors ?? throw new ArgumentNullException (nameof (accessors));
        }

        public StateAccessors StateAccessors { get; }

        public IStatePropertyAccessor<UserProfile> UserProfileAccessor { get => this.StateAccessors.UserProfileAccessor; }
        ```
    3.  因需要有三個prompt分別詢問姓名, 性別, 年齡, 所以加入3個 Dialog到這個Dialog中, 並透過WaterfallDialog設定對話步驟
        ```csharp
        
        public WelcomeDialog(StateAccessors accessors) : base(nameof(WelcomeDialog))
        {
            this.StateAccessors = accessors ?? throw new ArgumentNullException (nameof (accessors));
            
            var waterfallSteps = new WaterfallStep[]
            {
                InitializeStateStepAsync,
                PromptForNameStepAsync,
                PromptForGenderStepAsync,
                PromptForAgeStepAsync,
                DisplayUserProfileStepAsync,
            };
            AddDialog (new WaterfallDialog (nameof (WelcomeDialog), waterfallSteps));
            AddDialog (new TextPrompt ("name"));
            AddDialog (new ChoicePrompt ("gender"));
            AddDialog (new NumberPrompt<int> ("age"));
        }

        // 省略

        private async Task<DialogTurnResult> InitializeStateStepAsync (WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private async Task<DialogTurnResult> PromptForNameStepAsync (WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private async Task<DialogTurnResult> PromptForGenderStepAsync (WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private async Task<DialogTurnResult> PromptForAgeStepAsync (WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private async Task<DialogTurnResult> DisplayUserProfileStepAsync (WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
        ```
    4.  實作 `InitializeStateStepAsync` 方法, 首先先發出歡迎訊息, 並且前往下一個Step
        ```csharp
        private async Task<DialogTurnResult> InitializeStateStepAsync (WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            await stepContext.Context.SendActivityAsync (MessageFactory.Text ("歡迎使用Bot"));
  
            return await stepContext.NextAsync ();
        }
        ```
    5.  實作 `PromptForNameStepAsync` 方法, 建立詢問名字的提示
        ```csharp
        private async Task<DialogTurnResult> PromptForNameStepAsync (WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var opts = new PromptOptions
            {
                Prompt = MessageFactory.Text ("您的名字?")
            };
            return await stepContext.PromptAsync ("name", opts);
        }
        ```
    6.  實作 `PromptForGenderStepAsync` 方法, 將上一步驟的結果當作名字的結果, 先從 `UserProfileAccessor` 取得 `UserProfile`, 並且將 `UserName` 寫入, 存回 `UserProfileAccessor`, 最後建立詢問性別的提示
        ```csharp
        private async Task<DialogTurnResult> PromptForGenderStepAsync (WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userProfile = await this.UserProfileAccessor.GetAsync (stepContext.Context, () => new UserProfile ());
            var name = stepContext.Result as string;
            if (string.IsNullOrWhiteSpace (name) == true)
            {
                return await stepContext.ContinueDialogAsync ();
            }
            userProfile.UserName = name;
            await this.UserProfileAccessor.SetAsync (stepContext.Context, userProfile);

            var opts = new PromptOptions
            {
                Prompt = MessageFactory.Text ("您的性別?"),
                Choices = ChoiceFactory.ToChoices (new List<string> { "Male", "Female" })
            };
            return await stepContext.PromptAsync ("gender", opts);
        }
        ```
    7.  實作 `PromptForAgeStepAsync` 方法, 將上一步驟的結果看作是性別存回, 並且建立詢問年齡的提示.
        ```csharp
        private async Task<DialogTurnResult> PromptForAgeStepAsync (WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userProfile = await this.UserProfileAccessor.GetAsync (stepContext.Context, () => new UserProfile ());

            if (!(stepContext.Result is FoundChoice choice))
            {
                throw new InvalidOperationException("");
            }
            userProfile.Gender = choice.Index == 0;
            await this.UserProfileAccessor.SetAsync (stepContext.Context, userProfile);

            var opts = new PromptOptions
            {
                Prompt = MessageFactory.Text ("您的年齡?")
            };
            return await stepContext.PromptAsync ("age", opts);
        }
        ```
    7.  實作 `DisplayUserProfileStepAsync` 方法, 將上一步驟的結果看作是年齡存回, 並且將個人資訊回覆給使用者, 並且結束對話
        ```csharp
        private async Task<DialogTurnResult> DisplayUserProfileStepAsync (WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userProfile = await this.UserProfileAccessor.GetAsync (stepContext.Context, () => new UserProfile ());
            var age = (int) stepContext.Result;
            userProfile.Age = age;
            await this.UserProfileAccessor.SetAsync (stepContext.Context, userProfile);

            await stepContext.Context.SendActivityAsync (MessageFactory.Text ($"您是{userProfile.UserName}, {(userProfile.Gender ? "男" : "女")},   {userProfile.Age} 歲"));

            return await stepContext.EndDialogAsync (cancellationToken: cancellationToken);
        }
        ```
    8.  針對年齡建立驗證, 在 `AddDialog (new NumberPrompt<int> ("age"))` 的 `NumberPrompt` 加入第二個參數並實作
        ```csharp
        // 省略
           AddDialog (new NumberPrompt<int> ("age", AgeValidator));
           
        //省略
        private async Task<bool> AgeValidator (PromptValidatorContext<int> promptContext, CancellationToken cancellationToken)
        {
            var age = promptContext.Recognized.Value;

            if (age < 18 || age > 100)
            {
                await promptContext.Context.SendActivityAsync (MessageFactory.Text ("年齡需在 18 與 99 之間"));
                return false;
            }
            return true;
        }
        ```
6.  修改 `DemoBot` 在加入對話時, 先進入 `WelcomeDialog`, 首先設定Dialog進入DialogSet, 並在 `conversationUpdate` 事件時, 開始Dialog, 並且在 `message` 事件中若對話未結束就繼續對話
    1. 修改 `StartUp`, 註冊 `WelcomeDialog`
        ```csharp
        public void ConfigureServices (IServiceCollection services)
        {
            services.AddSingleton<WelcomeDialog>();
        }
        ```
    1. 建立 `DialogSet`, 注入並且加入剛剛建立的 `WelcomeDialog`
        ```csharp
        using Microsoft.Bot.Builder.Dialogs;
        
        public DemoBot (StateAccessors accessors, WelcomeDialog welcomeDialog)
        {
            // 省略
            Dialogs = new DialogSet (accessors.DialogStateAccessor);
            Dialogs.Add (welcomeDialog);
        }

        private DialogSet Dialogs { get; set; }
        ```
    2. 修改 `OnTurnAsync`方法, 建立 `DialogContext`
        ```csharp
        public async Task OnTurnAsync (ITurnContext turnContext, CancellationToken cancellationToken = default (CancellationToken))
        {
            var dc = await Dialogs.CreateContextAsync (turnContext);
            // todo: 判斷activity 的狀態, 做出對應動作, 下一步驟補齊
        }
    3. 延續上步驟的程式碼, 在 `dialogResult` 之後, 在 `ConversationUpdate` 事件時, 將 `DialogSet` 開始 Dialog
        ```csharp
        var activity = turnContext.Activity;

        if (activity.Type == ActivityTypes.Message) 
        {
            // todo: 延續上次的dialog, 取得 dialog 的result, 做出對應動作, 下一步驟補齊
        }
        else if (activity.Type == ActivityTypes.ConversationUpdate)
        {
           if (activity.MembersAdded.Any ())
           {
               foreach (var member in activity.MembersAdded)
               {
                   if (member.Id != activity.Recipient.Id)
                   {
                       await dc.BeginDialogAsync (nameof (WelcomeDialog));
                   }
               }
           }
        }
        ```
    4. 延續上步驟程式碼, 在 `Message` 事件時, 繼續 dialog, 並取得對話結果, 若是完成, 就將對話結束, 若對話狀態是Empty(表示沒有對話在執行), 則照上一案例回復
        ```csharp
        var dialogResult = await dc.ContinueDialogAsync();
        if (dialogResult.Status == DialogTurnStatus.Complete)
        {
            await dc.EndDialogAsync();
            await turnContext.SendActivityAsync(MessageFactory.Text("您可以開始跟我聊天"));
        }
        else if (dialogResult.Status == DialogTurnStatus.Empty)
        {
           var count = await this._accessors.CounterAccessor
               .GetAsync (turnContext, () => default (int), cancellationToken);
           var reply = activity.CreateReply ($"您說了: {activity.Text}, 這是您第 {++count} 次留言");
           await turnContext.SendActivityAsync (reply);
           await this._accessors.CounterAccessor.SetAsync (turnContext, count, cancellationToken);
        }
        ```
    5. 在 `OnTurnAsync` 的最後補上儲存 `UserState` 跟 `ConversationState` 的動作, 讓 `UserProfile`、`DialogState`和 Counter可以被儲存
        ```csharp
        await this._accessors.ConversationState.SaveChangesAsync(turnContext);
        await this._accessors.UserState.SaveChangesAsync(turnContext);
        ```
# 4 - 說好的AI在哪裡? LUIS
## 目的
學習如何整合 LUIS 服務進 bot
## 實作目標
使用LUIS (Language Understanding Intelligent Service) 了解使用者說話的意圖, 並且給予對應的結果
## 步驟
1. 下載 `kkbox.luis.json` 到目錄 `CognitiveModels` 中
2. 使用 Microsoft Account 進入 http://luis.ai
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
    ```csharp
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
    ```
    > 都回傳狀態為 `DialogTurnStatus.Waiting` 的原因是, 讓Bot維持在 `KKBoxDialog` 中

8. 在 `StartUp.cs` 中, 驗證 `BotConfiguration` 是否有 LuisService, 並且註冊Service
    ```csharp
    using Microsoft.Bot.Builder.AI.Luis;

    public void ConfigureServices (IServiceCollection services)
    {
        // 省略
        var luisService = botConfig.Services.OfType<LuisService> ().FirstOrDefault ();

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
9. 修改 `DemoBot.cs`, 注入 `KKBoxDialog` 並且將 `KKBoxDialog` 加入`DialogSet`
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
12. 因為 `LuisRecognizer.RecognizeAsync` 的結果中, Entity屬性是 `JObject` 型別, 還是希望用強型別處理
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
    services.AddSingleton<KKBOX.OpenAPI.KKBOXAPI> (sp =>
    {
        var openApi = new KKBOX.OpenAPI.KKBOXAPI ();
        var clientId = "f0ae5ebb40eae042dc819852d30ea96c";
        var clientSecret = "86424de7030ffe62ae52d9d4769aa6f0";
        var authResult = KKBOX.OpenAPI.KKBOXOAuth.SignInAsync (clientId, clientSecret).Result;
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

        private async Task<IActivity> GetChartPlayListAsync (KKBoxRecognizerConvert result)
        {
            var charts = await this.Api.GetChartListAsync ();
            var chart = charts.Content.Data
                .First (p => p.Title.Contains (result.Entities.chart_type.FirstOrDefault ()) && p.Title.Contains (result.Entities.lang.FirstOrDefault ()));
            var playList = await this.Api.GetPlaylistOfChartAsync (chart.Id);
            var attachment = new ThumbnailCard (playList.Content.Title,
                playList.Content.UpdateAt,
                images : playList.Content.Images.Select (img => new CardImage (img.Url)).ToList (),
                tap : new CardAction ("openUrl", value : GetKKBoxPlayListUrl (playList.Content.Id))).ToAttachment ();
            var activity = MessageFactory.Attachment (attachment);
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
<!--
    ```csharp
    public class KKBoxDialog : Dialog
    {
        public KKBoxDialog (LuisRecognizer luisRecognizer, KKBOXAPI api) : base (nameof (KKBoxDialog))
        {
            LuisRecognizer = luisRecognizer;
            Api = api;
        }

        public LuisRecognizer LuisRecognizer { get; }
        public KKBOXAPI Api { get; }

        public override async Task<DialogTurnResult> BeginDialogAsync (DialogContext dc,
            object options = null,
            CancellationToken cancellationToken = default (CancellationToken))
        {
            return await dc.ContinueDialogAsync (cancellationToken);
        }

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

        private async Task<IActivity> GetSearchResultAsync (KKBoxRecognizerConvert result)
        {
            var queryResult = await this.Api.SearchAsync(result.Entities.keyword[0]);
            var attachments = queryResult.Content.Albums.Data
                .Select(p => 
                    new ThumbnailCard(p.Name, 
                        p.ReleaseDate, 
                        images: p.Images.Select(img => new CardImage(img.Url)).ToList()).ToAttachment());
            var activity = MessageFactory.Carousel(attachments);
            return activity;
        }

        private async Task<IActivity> GetChartPlayListAsync (KKBoxRecognizerConvert result)
        {
            var charts = await this.Api.GetChartListAsync();
            var chart = charts.Content.Data
                .First(p => p.Title.Contains(result.Entities.chart_type.FirstOrDefault()) && p.Title.Contains(result.Entities.lang.FirstOrDefault()));
            var playList = await this.Api.GetPlaylistOfChartAsync(chart.Id);
            var attachment = new ThumbnailCard(playList.Content.Title,
                playList.Content.UpdateAt,
                images: playList.Content.Images.Select(img => new CardImage(img.Url)).ToList()).ToAttachment();
            var activity = MessageFactory.Attachment(attachment);
            return activity;
        }
    }
    ```
-->