    using Microsoft.Bot.Builder.Integration.AspNet.Core;
    using Microsoft.Bot.Configuration;
    using Microsoft.Bot.Connector.Authentication;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    // 忽略中間
        private bool _isProduction;
        private ILoggerFactory _loggerFactory;

        public Startup(IConfiguration configuration) 
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services) 
        {
            var secretKey = Configuration.GetSection("botFileSecret")?.Value;
            var botFilePath = Configuration.GetSection("botFilePath")?.Value;

            var botConfig = BotConfiguration.Load(botFilePath ?? "./BotFramework.bot", secretKey);

            services.AddSingleton(sp => botConfig ??
                throw new InvalidOperationException($"The .bot config file could not be loaded. ({botConfig})"));

            var environment = _isProduction ? "production" : "development";
            var service = botConfig.Services.Where(s => s.Type == "endpoint" && s.Name == environment).FirstOrDefault();

            if (!(service is EndpointService endpointService)) 
            {
                throw new InvalidOperationException($"The .bot file does not contain an endpoint with name '{environment}'.");
            }

            services.AddBot<DemoBot>(options => 
            {

                options.CredentialProvider = new SimpleCredentialProvider(endpointService.AppId, endpointService.AppPassword);

                ILogger logger = _loggerFactory.CreateLogger<DemoBot>();

                options.OnTurnError = async (context, exception) => 
                {
                    logger.LogError($"Exception caught : {exception}");
                    await context.SendActivityAsync("Sorry, it looks like something went wrong.");
                };
            });
        }

        public void Configure(IApplicationBuilder app, 
            IHostingEnvironment env, 
            ILoggerFactory loggerFactory) 
        {
            this._isProduction = env.IsProduction();
            this._loggerFactory = loggerFactory;

            if (env.IsDevelopment()) 
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseDefaultFiles()
               .UseStaticFiles()
               .UseBotFramework();
        }
    // 省略
