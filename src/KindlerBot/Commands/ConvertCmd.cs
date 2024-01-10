using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using KindlerBot.Configuration;
using KindlerBot.Conversion;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace KindlerBot.Commands;

internal record ConvertCmdRequest(Document Doc, Chat Chat) : IRequest;

internal class ConvertCmdHandler : IRequestHandler<ConvertCmdRequest>
{
    /// <summary>
    /// Get list here: https://www.amazon.com/gp/help/customer/display.html?nodeId=G5WYD9SAF7PGXRNA
    /// </summary>
    private static readonly HashSet<string> KindleSupportedFormats = new(
        new[]
        {
            ".DOC", ".DOCX",
            ".HTML", ".HTM",
            ".RTF",
            ".TXT",
            ".JPEG", ".JPG",
            ".GIF",
            ".PNG",
            ".BMP",
            ".PDF",
            ".EPUB"
            // ".MOBI", ".AZW", // Is deprecated
        },
        StringComparer.OrdinalIgnoreCase);

    private readonly ITelegramBotClient _botClient;
    private readonly IConfigStore _configStore;
    private readonly ICalibreCli _calibreCli;
    private readonly DebugConfiguration _debugConfiguration;
    private readonly ILogger<ConvertCmdHandler> _logger;

    public ConvertCmdHandler(ITelegramBotClient botClient,
        IConfigStore configStore,
        ICalibreCli calibreCli,
        IOptions<DebugConfiguration> conversionConfig,
        ILogger<ConvertCmdHandler> logger)
    {
        _botClient = botClient;
        _configStore = configStore;
        _calibreCli = calibreCli;
        _debugConfiguration = conversionConfig.Value;
        _logger = logger;
    }

    public async Task Handle(ConvertCmdRequest request, CancellationToken cancellationToken)
    {
        var email = await _configStore.GetChatEmail(request.Chat.Id);
        if (email == null)
        {
            await _botClient.SendTextMessageAsync(request.Chat, "❌ Email is not configured. Fix it and try again!", cancellationToken: cancellationToken);
            return;
        }

        // Start conversion in background as it could take long time.
        _ = Task.Run(() => ConvertDocument(request.Doc, request.Chat, email), CancellationToken.None);
    }

    private async Task ConvertDocument(Document doc, Chat chat, string email)
    {
        try
        {
            using var tempDir = new TempDir(chat.Id);
            if (_debugConfiguration.KeepConversionTempWorkDir)
            {
                tempDir.SuppressCleanup();
            }

            await _botClient.SendTextMessageAsync(chat, "⏬ Downloading file...");
            var fileInfo = await _botClient.GetFileAsync(doc.FileId);

            var sourceFilePath = Path.Join(tempDir.DirPath, doc.FileName);
            await using (var sourceFileStream = System.IO.File.Create(sourceFilePath))
            {
                await _botClient.DownloadFileAsync(fileInfo.FilePath!, sourceFileStream);
            }
            await _botClient.SendTextMessageAsync(chat, "✅ Downloaded!");

            var bookInfo = await _calibreCli.GetBookInfo(sourceFilePath);
            if (bookInfo.IsSuccessful)
            {
                await _botClient.SendTextMessageAsync(chat, $"📖 Book info\nTitle: {bookInfo.Value.Title}\nAuthor: {bookInfo.Value.Author}");
            }
            else
            {
                await _botClient.SendTextMessageAsync(chat, $"⚠ Unable to get book metadata from the file you sent. Error: {bookInfo.Error}");
            }

            var bookCoverPath = sourceFilePath + ".cover.jpg";
            var hasCover = await _calibreCli.ExportCover(sourceFilePath, bookCoverPath);
            if (hasCover.IsSuccessful)
            {
                await using var coverFileStream = System.IO.File.OpenRead(bookCoverPath);
                await _botClient.SendPhotoAsync(chat, new InputFileStream(coverFileStream, Path.GetFileName(bookCoverPath)));
            }

            string convertedFilePath;
            bool convertedBook;
            if (KindleSupportedFormats.Contains(Path.GetExtension(doc.FileName!)))
            {
                convertedFilePath = sourceFilePath;
                convertedBook = false;
                await _botClient.SendTextMessageAsync(chat, $"ℹ Conversion skipped for {Path.GetExtension(doc.FileName)}");
            }
            else
            {
                await _botClient.SendTextMessageAsync(chat, "🔃 Converting book...");

                convertedFilePath = sourceFilePath + ".epub";
                convertedBook = true;
                var conversionResult = await _calibreCli.ConvertBook(sourceFilePath, convertedFilePath);
                if (conversionResult.IsSuccessful)
                {
                    await _botClient.SendTextMessageAsync(chat, $"✔ Converted to KINDLE (.epub) format!");
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

            _logger.LogInformation(
                convertedBook ? "Converted and sent book. Book name: {book}, User: {user}" : "Sent book without conversion. Book name: {book}, User: {user}",
                doc.FileName, chat.Username);
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
