1.  建立類別 `WelcomeDialog`, 繼承自 `Microsoft.Bot.Builder.Dialogs.ComponentDialog`, 需要實作一個參數的建構子, 參數為Dialog 名稱
   
    ```csharp  
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Threading;
    using System;
    using Microsoft.Bot.Builder.Dialogs.Choices;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder;

    namespace DemoBotApp
    {
        public class WelcomeDialog : ComponentDialog
        {
            public WelcomeDialog() : base(nameof(WelcomeDialog)) {}
        }
    }
    ```

2.  會用到 `StateAccessors`, 所以在建構子加入型別為 StateAccessors 的參數, 並且寫入屬性

    ```csharp
    public WelcomeDialog(StateAccessors accessors) : base(nameof(WelcomeDialog))
    {
        this.StateAccessors = accessors ?? throw new ArgumentNullException(nameof(accessors));
    }

    public StateAccessors StateAccessors { get; }

    public IStatePropertyAccessor<UserProfile> UserProfileAccessor => this.StateAccessors.UserProfileAccessor;
    ```

3.  因需要有三個 prompt 分別詢問姓名, 性別, 年齡, 所以加入3個  Dialog 到這個 Dialog 中, 並透過W aterfallDialog 設定對話步驟

    ```csharp
    
    public WelcomeDialog(StateAccessors accessors) : base(nameof(WelcomeDialog))
    {
        this.StateAccessors = accessors ?? throw new ArgumentNullException(nameof(accessors));
        
        var waterfallSteps = new WaterfallStep[]
        {
            InitializeStateStepAsync,
            PromptForNameStepAsync,
            PromptForGenderStepAsync,
            PromptForAgeStepAsync,
            DisplayUserProfileStepAsync,
        };
        AddDialog(new WaterfallDialog(nameof(WelcomeDialog), waterfallSteps));
        AddDialog(new TextPrompt("name"));
        AddDialog(new ChoicePrompt("gender"));
        AddDialog(new NumberPrompt<int>("age"));
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

4.  實作 `InitializeStateStepAsync` 方法, 首先先發出歡迎訊息, 並且前往下一個 Step

    ```csharp
    private async Task<DialogTurnResult> InitializeStateStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        await stepContext.Context.SendActivityAsync(MessageFactory.Text("歡迎使用Bot"));

        return await stepContext.NextAsync();
    }
    ```

5.  實作 `PromptForNameStepAsync` 方法, 建立詢問名字的提示

    ```csharp
    private async Task<DialogTurnResult> PromptForNameStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        var opts = new PromptOptions
        {
            Prompt = MessageFactory.Text("您的名字?")
        };
        return await stepContext.PromptAsync("name", opts);
    }
    ```

6.  實作 `PromptForGenderStepAsync` 方法, 將上一步驟的結果當作名字的結果, 先從 `UserProfileAccessor` 取得 `UserProfile`, 並且將 `UserName` 寫入, 存回 `UserProfileAccessor`, 最後建立詢問性別的提示

    ```csharp
    private async Task<DialogTurnResult> PromptForGenderStepAsync (WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        var userProfile = await this.UserProfileAccessor.GetAsync(stepContext.Context, () => new UserProfile ());
        var name = stepContext.Result as string;
        if (string.IsNullOrWhiteSpace(name) == true)
        {
            return await stepContext.ContinueDialogAsync();
        }
        userProfile.UserName = name;
        await this.UserProfileAccessor.SetAsync(stepContext.Context, userProfile);

        var opts = new PromptOptions
        {
            Prompt = MessageFactory.Text("您的性別?"),
            Choices = ChoiceFactory.ToChoices(new List<string> { "Male", "Female" })
        };
        return await stepContext.PromptAsync("gender", opts);
    }
    ```

7.  實作 `PromptForAgeStepAsync` 方法, 將上一步驟的結果看作是性別存回, 並且建立詢問年齡的提示.

    ```csharp
    private async Task<DialogTurnResult> PromptForAgeStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        var userProfile = await this.UserProfileAccessor.GetAsync(stepContext.Context, () => new UserProfile ());

        if (!(stepContext.Result is FoundChoice choice))
        {
            throw new InvalidOperationException("");
        }
        userProfile.Gender = choice.Index == 0;
        await this.UserProfileAccessor.SetAsync(stepContext.Context, userProfile);

        var opts = new PromptOptions
        {
            Prompt = MessageFactory.Text("您的年齡?")
        };
        return await stepContext.PromptAsync("age", opts);
    }
    ```

8.  實作 `DisplayUserProfileStepAsync` 方法, 將上一步驟的結果看作是年齡存回, 並且將個人資訊回覆給使用者, 並且結束對話

    ```csharp
    private async Task<DialogTurnResult> DisplayUserProfileStepAsync (WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        var userProfile = await this.UserProfileAccessor.GetAsync(stepContext.Context, () => new UserProfile());
        var age = (int) stepContext.Result;
        userProfile.Age = age;
        await this.UserProfileAccessor.SetAsync(stepContext.Context, userProfile);

        await stepContext.Context.SendActivityAsync(MessageFactory.Text ($"您是{userProfile.UserName}, {(userProfile.Gender ? "男" : "女")},   {userProfile.Age} 歲"));

        return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
    }
    ```

9.  針對年齡建立驗證, 在 `AddDialog (new NumberPrompt<int> ("age"))` 的 `NumberPrompt` 加入第二個參數並實作

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
