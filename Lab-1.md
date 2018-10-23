# Lab-1. 建立一個會回覆你一樣的話的機器人

## 目的
學習如何建立會運作的 chatbot

## 實作目標
建立一個你說什麼話, 他就回你什麼話的討厭鬼

## 步驟

1. 確定工作目錄為 D:\

2. 透過 dotnet cli 建立專案  
   ```sh
   dotnet new web -n DemoBotApp
   ```

3. 透過 msbot 建立 .bot file  
   ```sh
   cd DemoBotApp
   msbot init -n DemoBot -q
   ```

   > * .bot file 的目的除了是程式碼針對 bot 的 configuration 外, 也是提供給 botframework emulator 的描述檔

4. 新增連接 endpoint 到 bot file  
   ```sh
   msbot connect endpoint -n development -e http://localhost:5000/api/messages
   ```

   > * endpoint 是告訴程式碼, 我的 Microsoft Id, Key 分別是什麼, 同時也是讓 botframework emulator 知道 bot 的 endpoint  
   > * port 使用 5000 是因為 dotnet cli 預設模板使用 5000 的 port, 當然可以修改
   > * 事實上 init 時有指定 -e 參數時, 也會建立一筆 endpoint, 但 名稱會同檔案名稱

5. 加入 nuget 套件  
   ```sh
   dotnet add package Microsoft.Bot.Builder
   dotnet add package Microsoft.Bot.Builder.Integration.AspNet.Core
   dotnet add package Microsoft.Bot.Configuration
   ```

5. 執行套件安裝指令
   ```sh
   dotnet restore
   ```

6. 在專案中增加 DemoBot 類別繼承自 IBot 並且實作 OnTurnAsync 方法  
   [DemoBot.cs](code/DemoBot.cs)

7. 建立 `appsettings.json`, 設定 botFileSecret 跟 botFilePath   
   ```json
   {
     "botFileSecret": "",
     "botFilePath": "DemoBot.bot"
   }
   ```

8. 在 Startup.cs 中, 註冊 service, 使用 middleware  
   [Statrup.cs](code/Statrup.cs)

9.  執行 ASP.NET Core 應用程式
   ```sh
   dotnet run
   ```

10. 執行 Bot Framework Emulator v4 (Preview), 驗證對話是否完成

11. 在 Azure 中新增 Bot Service

12. 新增連接 endpoint 到 bot file  
   ```sh
   msbot connect endpoint -n production -e https://xxx.azurewebsites.net/api/messages -a "appId" -p "appKey"
   ```
