using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KindlerBot.Configuration;
using KindlerBot.Conversion;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Extensions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace KindlerBot.Commands;

internal record ConvertCmdRequest(Document Doc, string? Caption, Chat Chat) : IRequest;

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
            await _botClient.SendMessage(request.Chat, "❌ Email is not configured. Fix it and try again!", cancellationToken: cancellationToken);
            return;
        }

        // Start conversion in background as it could take long time.
        _ = Task.Run(() => ConvertDocument(request.Doc, request.Caption, request.Chat, email), CancellationToken.None);
    }

    private async Task ConvertDocument(Document doc, string? docCaption, Chat chat, string email)
    {
        try
        {
            using var tempDir = new TempDir(chat.Id);
            if (_debugConfiguration.KeepConversionTempWorkDir)
            {
                tempDir.SuppressCleanup();
            }

            await _botClient.SendMessage(chat, "⏬ Downloading file...");
            var docFileInfo = await _botClient.GetFile(doc.FileId);

            var filePath = Path.Join(tempDir.DirPath, doc.FileName);
            await using (var fileStream = File.Create(filePath))
            {
                await _botClient.DownloadFile(docFileInfo.FilePath!, fileStream);
            }
            await _botClient.SendMessage(chat, "✅ Downloaded!");

            var bookInfo = await _calibreCli.GetBookInfo(filePath);
            if (bookInfo.IsSuccessful)
            {
                var bookTitle = bookInfo.Value.Title;
                var bookAuthor = Regex.Replace(bookInfo.Value.Author, @"\[.*\]$", "").Trim();

                // If a caption was specified, use it as the book file name
                var newFileName = $"{docCaption ?? bookTitle}{Path.GetExtension(filePath)}";
                var newFilePath = Path.Join(tempDir.DirPath, ReplaceInvalidPathChars(newFileName));

                await _botClient.SendMessage(chat, $"📖 *{Markdown.Escape(newFileName)}*\nTitle: {Markdown.Escape(bookTitle)}\nAuthor: {Markdown.Escape(bookAuthor)}", ParseMode.MarkdownV2);

                if (!string.Equals(filePath, newFilePath, StringComparison.Ordinal))
                {
                    File.Copy(filePath, newFilePath);
                    filePath = newFilePath;
                }

                static string ReplaceInvalidPathChars(string filename)
                {
                    return string.Join("", filename.Split(Path.GetInvalidFileNameChars()));
                }
            }
            else
            {
                await _botClient.SendMessage(chat, $"⚠ Unable to get book metadata from the file you sent. Error: {bookInfo.Error}");
            }

            var bookCoverPath = filePath + ".cover.jpg";
            var hasCover = await _calibreCli.ExportCover(filePath, bookCoverPath);
            if (hasCover.IsSuccessful)
            {
                await using var coverFileStream = File.OpenRead(bookCoverPath);
                await _botClient.SendPhoto(chat, new InputFileStream(coverFileStream, Path.GetFileName(bookCoverPath)));
            }

            string convertedFilePath;
            bool convertedBook;
            if (KindleSupportedFormats.Contains(Path.GetExtension(doc.FileName!)))
            {
                convertedFilePath = filePath;
                convertedBook = false;
                await _botClient.SendMessage(chat, $"ℹ Conversion skipped for {Path.GetExtension(doc.FileName)}");
            }
            else
            {
                await _botClient.SendMessage(chat, "🔃 Converting book...");

                convertedFilePath = filePath + ".epub";
                convertedBook = true;
                var conversionResult = await _calibreCli.ConvertBook(filePath, convertedFilePath);
                if (conversionResult.IsSuccessful)
                {
                    await _botClient.SendMessage(chat, $"✔ Converted to KINDLE (.epub) format!");
                }
                else
                {
                    await _botClient.SendMessage(chat, $"😢 Conversion failed. Error: {conversionResult.Error}");
                    return;
                }
            }

            await _botClient.SendMessage(chat, $"💌 Sending to your Kindle device...");
            var sendResult = await _calibreCli.SendBookToEmail(convertedFilePath, email);
            if (!sendResult.IsSuccessful)
            {
                await _botClient.SendMessage(chat, $"😢 Failed to send to Kindle. Error: {sendResult.Error}", linkPreviewOptions: new LinkPreviewOptions() { IsDisabled = true });
                return;
            }

            await _botClient.SendMessage(chat, $"🎉 Successfully sent your book!");

            _logger.LogInformation(
                convertedBook ? "Converted and sent book. Book name: {book}, User: {user}" : "Sent book without conversion. Book name: {book}, User: {user}",
                Path.GetFileNameWithoutExtension(filePath), chat.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Book conversion failed");
            await _botClient.SendMessage(chat, $"❌ Unexpected bot error when converting and sending book: {ex.Message}");
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
