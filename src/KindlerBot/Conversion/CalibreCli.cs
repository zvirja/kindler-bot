using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KindlerBot.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KindlerBot.Conversion;

internal class CalibreCli : ICalibreCli
{
    private readonly ICalibreCliExec _cliExec;
    private readonly SmtpConfiguration _smtpConfiguration;
    private readonly ILogger<CalibreCli> _logger;

    public CalibreCli(ICalibreCliExec cliExec, IOptions<SmtpConfiguration> smtpConfiguration, ILogger<CalibreCli> logger)
    {
        _cliExec = cliExec;
        _smtpConfiguration = smtpConfiguration.Value;
        _logger = logger;
    }

    public async Task<CalibreResult<BookInfo>> GetBookInfo(string filePath)
    {
        var (exitCode, output) = await _cliExec.RunCalibre(CalibreCliApp.Meta, new[] { Quote(filePath) });

        const string unknown = "<unknown>";

        // Calibre could return values even if reports an error. For instance, for PDF
        var title = GetKeyedOutputValue("Title", output);
        var author = GetKeyedOutputValue("Author(s)", output);
        if (title != null || author != null)
        {
            return CalibreResult<BookInfo>.Successful(new BookInfo(title ?? unknown, author ?? unknown));
        }

        var error = GetCalibreError(output);
        if (exitCode != 0 || error != null)
        {
            _logger.LogWarning("GetBookInfo failed. Exit code: {exit code}, output: {output}", exitCode, output);
            return CalibreResult<BookInfo>.Failed(error ?? "Calibre exited with error code");
        }

        return CalibreResult<BookInfo>.Successful(new BookInfo(title ?? unknown, author ?? unknown));
    }

    public async Task<CalibreResult> ExportCover(string filePath, string coverOutputPath)
    {
        var (exitCode, output) = await _cliExec.RunCalibre(CalibreCliApp.Meta, new[] { Quote(filePath), "--get-cover", Quote(coverOutputPath) });
        var error = GetCalibreError(output);
        if (exitCode != 0 || error != null)
        {
            _logger.LogWarning("ExportCover failed. Exit code: {exit code}, output: {output}", exitCode, output);
            return CalibreResult.Failed(error ?? "Calibre exited with error code");
        }

        if (output.Any(x => x.Contains("No cover found")))
        {
            return CalibreResult.Failed("No cover found");
        }

        if (output.LastOrDefault()?.StartsWith("Cover saved to") != true)
        {
            return CalibreResult.Failed("Cover was not saved");
        }

        return CalibreResult.Successful;
    }

    public async Task<CalibreResult> ConvertBook(string sourcePath, string destPath)
    {
        var (exitCode, output) = await _cliExec.RunCalibre(CalibreCliApp.Convert, new[] { Quote(sourcePath), Quote(destPath), "--output-profile kindle" });
        var error = GetCalibreError(output);
        if (exitCode != 0 || error != null)
        {
            _logger.LogWarning("ConvertBook failed. Exit code: {exit code}, output: {output}", exitCode, output);
            return CalibreResult.Failed(error ?? "Calibre exited with error code");
        }

        return CalibreResult.Successful;
    }

    public async Task<CalibreResult> SendBookToEmail(string filePath, string email)
    {
        string fileName = Path.GetFileName(filePath);

        var (exitCode, output) = await _cliExec.RunCalibre(
            CalibreCliApp.Smtp,
            new[]
            {
                "--subject", Quote(fileName),
                "--attachment", Quote(filePath),
                "--relay", Quote(_smtpConfiguration.RelayServer),
                "--port", _smtpConfiguration.Port.ToString(),
                "--encryption-method", _smtpConfiguration.RelayServerEncryption,
                "--username", Quote(_smtpConfiguration.UserName),
                "--password", Quote(_smtpConfiguration.Password),
                _smtpConfiguration.FromEmail, // from
                email, // to
                Quote("Send to kindle") // text
            });

        var error = output.LastOrDefault(x => x.Contains("Error"))?.Split(':', 2)[^1];
        if (exitCode != 0 || error != null)
        {
            _logger.LogWarning("SendBookToEmail failed. Exit code: {exit code}, output: {output}", exitCode, output);
            return CalibreResult.Failed(error ?? "Calibre exited with error code");
        }

        return CalibreResult.Successful;
    }

    private static string? GetKeyedOutputValue(string key, string[] output)
    {
        return output.FirstOrDefault(x => x.StartsWith(key))?.Split(':', 2)[1].Trim();
    }

    private static string? GetCalibreError(string[] output) => GetKeyedOutputValue("ValueError", output);

    private static string Quote(string text) => $"\"{text}\"";
}
