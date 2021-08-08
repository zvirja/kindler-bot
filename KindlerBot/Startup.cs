using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using KindlerBot.Commands;
using KindlerBot.Configuration;
using KindlerBot.Conversion;
using KindlerBot.Security;
using KindlerBot.Services;
using KindlerBot.Workflow;
using MediatR;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace KindlerBot
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers().AddNewtonsoftJson();

            services.AddHostedService<WebhookConfiguration>();
            services.AddHostedService<CommandConfiguration>();


            services.AddOptions<BotConfiguration>()
                .BindConfiguration(BotConfiguration.SectionName)
                .PostConfigure(config =>
                {
                    if (config.WebhookUrlSecret == null)
                    {
                        config.WebhookUrlSecret = Guid.NewGuid().ToString("N");
                    }
                });
            services.AddOptions<DeploymentConfiguration>().BindConfiguration(DeploymentConfiguration.SectionName);
            services.AddOptions<SmtpConfiguration>().BindConfiguration(SmtpConfiguration.SectionName);
            services.AddOptions<CalibreCliConfiguration>().BindConfiguration(CalibreCliConfiguration.SectionName);
            services.AddOptions<ConversionConfiguration>().BindConfiguration(ConversionConfiguration.SectionName);

            services.AddHttpClient<ITelegramBotClient, TelegramBotClient>((httpClient, sp) =>
                new TelegramBotClient(sp.GetRequiredService<IOptions<BotConfiguration>>().Value.BotToken, httpClient));

            services.AddMediatR(typeof(Startup));

            services.AddSingleton<IChatAuthorization, ChatAuthorization>();
            services.AddSingleton<ITelegramCommands, TelegramCommands>();
            services.AddSingleton<IWorkflowManager, WorkflowManager>();
            services.AddSingleton<IConfigStore, FileSystemConfigStore>();
            services.AddSingleton<ICalibreCli, CalibreCli>();
            services.AddSingleton<ICalibreCliExec, CalibreCliExec>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IOptions<DeploymentConfiguration> deployConfig)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            var deployUrlSubPath = deployConfig.Value.PublicUrl.LocalPath;
            if (!string.IsNullOrEmpty(deployUrlSubPath))
            {
                app.UsePathBase(deployUrlSubPath);
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
