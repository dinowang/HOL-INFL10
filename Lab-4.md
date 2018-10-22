# Lab-4. 說好的 AI 在哪裡? LUIS
   
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
