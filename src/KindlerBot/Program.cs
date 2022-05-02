using System;
using System.Reflection;
using KindlerBot.Commands;
using KindlerBot.Configuration;
using KindlerBot.Conversion;
using KindlerBot.Interactivity;
using KindlerBot.Security;
using KindlerBot.Services;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration);
    lc.WriteTo.Console();
});

builder.Services.AddHostedService<WebhookConfiguration>();
builder.Services.AddHostedService<CommandConfiguration>();
builder.Services.AddHostedService<UpdateNotifier>();

builder.Services.AddOptions<BotConfiguration>().BindConfiguration(BotConfiguration.SectionName)
    .Configure(config =>
    {
        if (config.WebhookUrlSecret == null)
        {
            config.WebhookUrlSecret = Guid.NewGuid().ToString("N");
        }
    });
builder.Services.AddOptions<DeploymentConfiguration>().BindConfiguration(DeploymentConfiguration.SectionName);
builder.Services.AddOptions<SmtpConfiguration>().BindConfiguration(SmtpConfiguration.SectionName);
builder.Services.AddOptions<CalibreCliConfiguration>().BindConfiguration(CalibreCliConfiguration.SectionName);
builder.Services.AddOptions<ConversionConfiguration>().BindConfiguration(ConversionConfiguration.SectionName);

builder.Services.AddHttpClient<ITelegramBotClient, TelegramBotClient>((httpClient, sp) =>
    new TelegramBotClient(sp.GetRequiredService<IOptions<BotConfiguration>>().Value.BotToken, httpClient));

builder.Services.AddMediatR(Assembly.GetExecutingAssembly());

builder.Services.AddSingleton<IChatAuthorization, ChatAuthorization>();
builder.Services.AddSingleton<ITelegramCommands, TelegramCommands>();
builder.Services.AddSingleton<IInteractionManager, InteractionManager>();
builder.Services.AddSingleton<IConfigStore, FileSystemConfigStore>();
builder.Services.AddSingleton<ICalibreCli, CalibreCli>();
builder.Services.AddSingleton<ICalibreCliExec, CalibreCliExec>();

builder.Services.AddControllers().AddNewtonsoftJson();

var app = builder.Build();

var deployUrlSubPath = app.Services.GetRequiredService<IOptions<DeploymentConfiguration>>().Value.PublicUrl.LocalPath;
if (!string.IsNullOrEmpty(deployUrlSubPath))
{
    app.UsePathBase(deployUrlSubPath);
}

app.MapControllers();

await app.RunAsync();
