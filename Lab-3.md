# Lab-3. 使用 Dialog 做到更彈性的機器人

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
   [WelcomeDialog.cs](Lab-3-WelcomeDialog.md)

6. 修改 `DemoBot` 在加入對話時, 先進入 `WelcomeDialog`, 首先設定 Dialog 進入 DialogSet, 並在 `conversationUpdate` 事件時, 開始 Dialog, 並且在 `message` 事件中若對話未結束就繼續對話
   [DialogSet.cs](Lab-3-DialogSet.md)
