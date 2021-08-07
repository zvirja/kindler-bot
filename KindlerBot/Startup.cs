using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using KindlerBot.Commands;
using KindlerBot.Configuration;
using KindlerBot.Services;
using KindlerBot.Workflow;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace KindlerBot
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers().AddNewtonsoftJson();

            services.AddHostedService<WebhookConfiguration>();
            services.AddHostedService<CommandConfiguration>();

            services.Configure<BotConfiguration>(_configuration.GetSection(BotConfiguration.SectionName));
            services.PostConfigure<BotConfiguration>(config =>
            {
                if (config.WebhookUrlSecret == null)
                {
                    config.WebhookUrlSecret = Guid.NewGuid().ToString("N");
                }
            });

            services.AddHttpClient<ITelegramBotClient, TelegramBotClient>((httpClient, sp) =>
                new TelegramBotClient(sp.GetRequiredService<IOptions<BotConfiguration>>().Value.BotToken, httpClient));

            services.AddMediatR(typeof(Startup));

            services.AddSingleton<ITelegramCommands, TelegramCommands>();
            services.AddSingleton<IWorkflowManager, WorkflowManager>();
            services.AddSingleton<IConfigurationManager, ConfigurationManager>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
