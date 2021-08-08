﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KindlerBot.Configuration;
using KindlerBot.Conversion;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace KindlerBot.Commands
{
    internal record ConvertCmdRequest(Document Doc, Chat Chat) : IRequest;

    internal class ConvertCmdHandler : IRequestHandler<ConvertCmdRequest>
    {
        private readonly ITelegramBotClient _botClient;
        private readonly IConfigStore _configStore;
        private readonly ICalibreCli _calibreCli;
        private readonly ConversionConfiguration _conversionConfig;
        private readonly ILogger<ConvertCmdHandler> _logger;

        public ConvertCmdHandler(ITelegramBotClient botClient,
            IConfigStore configStore,
            ICalibreCli calibreCli,
            IOptions<ConversionConfiguration> conversionConfig,
            ILogger<ConvertCmdHandler> logger)
        {
            _botClient = botClient;
            _configStore = configStore;
            _calibreCli = calibreCli;
            _conversionConfig = conversionConfig.Value;
            _logger = logger;
        }

        public async Task<Unit> Handle(ConvertCmdRequest request, CancellationToken cancellationToken)
        {
            var email = await _configStore.GetChatEmail(request.Chat.Id);
            if (email == null)
            {
                await _botClient.SendTextMessageAsync(request.Chat, "❌ Email is not configured. Fix it and try again!", cancellationToken: cancellationToken);
                return Unit.Value;
            }

            // Start conversion in background as it could take long time.
            _ = Task.Run(() => ConvertDocument(request.Doc, request.Chat, email), CancellationToken.None);

            return Unit.Value;
        }

        private async Task ConvertDocument(Document doc, Chat chat, string email)
        {
            try
            {
                using var tempDir = new TempDir(chat.Id);
                if (_conversionConfig.KeepTempWorkDir)
                {
                    tempDir.SuppressCleanup();
                }

                await _botClient.SendTextMessageAsync(chat, "⏬ Downloading file...");
                var fileInfo = await _botClient.GetFileAsync(doc.FileId);

                var sourceFilePath = Path.Join(tempDir.DirPath, doc.FileName);
                await using (var sourceFileStream = System.IO.File.Create(sourceFilePath))
                {
                    await _botClient.DownloadFileAsync(fileInfo.FilePath, sourceFileStream);
                }

                var bookInfo = await _calibreCli.GetBookInfo(sourceFilePath);
                if (!bookInfo.IsSuccessful)
                {
                    await _botClient.SendTextMessageAsync(chat, $"😢 Unable to find book in the file you sent. Error: {bookInfo.Error}");
                    return;
                }

                await _botClient.SendTextMessageAsync(chat, $"☑ Downloaded!\nTitle: {bookInfo.Value.Title}\nAuthor: {bookInfo.Value.Author}");

                var bookCoverPath = sourceFilePath + ".cover.jpg";
                var hasCover = await _calibreCli.ExportCover(sourceFilePath, bookCoverPath);
                if (hasCover.IsSuccessful)
                {
                    await using var coverFileStream = System.IO.File.OpenRead(bookCoverPath);
                    await _botClient.SendPhotoAsync(chat, new InputMedia(coverFileStream, Path.GetFileName(bookCoverPath)));
                }

                string convertedFilePath;
                if (doc.FileName.EndsWith(".pdf"))
                {
                    convertedFilePath = sourceFilePath;
                    await _botClient.SendTextMessageAsync(chat, $"ℹ Conversion skipped for PDF");
                }
                else
                {
                    await _botClient.SendTextMessageAsync(chat, "🔃 Converting book...");

                    convertedFilePath = sourceFilePath + ".mobi";
                    var conversionResult = await _calibreCli.ConvertBook(sourceFilePath, convertedFilePath);
                    if (conversionResult.IsSuccessful)
                    {
                        await _botClient.SendTextMessageAsync(chat, $"✔ Converted to MOBI.");
                    }
                    else
                    {
                        await _botClient.SendTextMessageAsync(chat, $"😢 Conversion failed. Error: {conversionResult.Error}");
                        return;
                    }
                }

                await _botClient.SendTextMessageAsync(chat, $"💌 Sending to your Kindle device...");
                var sendResult = await _calibreCli.SendBookToEmail(convertedFilePath, email);
                if (!sendResult.IsSuccessful)
                {
                    await _botClient.SendTextMessageAsync(chat, $"😢 Failed to send to Kindle. Error: {sendResult.Error}", disableWebPagePreview: true);
                    return;
                }

                await _botClient.SendTextMessageAsync(chat, $"🎉 Successfully sent your book!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Book conversion failed");
                await _botClient.SendTextMessageAsync(chat, $"❌ Unexpected bot error when converting and sending book: {ex.Message}");
            }
        }

        private class TempDir: IDisposable
        {
            private bool Clean { get; set; } = true;
            public string DirPath { get; }

            public TempDir(ChatId chatId)
            {
                DirPath = Path.Join(Path.GetTempPath(), $"Kindler_{chatId}_{DateTime.Now.Ticks}");
                Directory.CreateDirectory(DirPath);
            }

            public void SuppressCleanup() => Clean = false;

            public void Dispose()
            {
                if (Clean)
                {
                    Directory.Delete(DirPath, recursive: true);
                }
            }
        }
    }
}
