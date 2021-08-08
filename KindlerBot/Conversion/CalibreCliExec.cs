using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using KindlerBot.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KindlerBot.Conversion
{
    internal class CalibreCliExec : ICalibreCliExec
    {
        private readonly CalibreCliConfiguration _cliConfig;
        private readonly ILogger<CalibreCliExec> _logger;

        public CalibreCliExec(IOptions<CalibreCliConfiguration> cliConfig, ILogger<CalibreCliExec> logger)
        {
            _logger = logger;
            _cliConfig = cliConfig.Value;
        }

        public async Task<(int exitCode, string[] output)> RunCalibre(CalibreCliApp app, string[] args)
        {
            var appName = app switch
            {
                CalibreCliApp.Meta => "ebook-meta",
                CalibreCliApp.Convert => "ebook-convert",
                CalibreCliApp.Smtp => "calibre-smtp",
                _ => throw new ArgumentOutOfRangeException(nameof(app), app, null)
            };
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                appName += ".exe";
            }

            var exePath = Path.Join(_cliConfig.HomeDir, appName);
            var argsLine = string.Join(" ", args);
            var psi = new ProcessStartInfo(exePath, argsLine);

            psi.WorkingDirectory = Path.GetTempPath();

            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            _logger.LogDebug("Running CalibreCli: {path} {args}", exePath, argsLine);
            var process = Process.Start(psi) ?? throw new InvalidOperationException($"Unable to start Calibre CLI process. Tool: {appName}");

            await process.WaitForExitAsync();

            var output = await process.StandardOutput.ReadToEndAsync();
            var outputLines = output.Split('\n').Select(x => x.Trim()).ToArray();

            _logger.LogDebug("CalibreCli exited with {exit code}. Output: {output}", process.ExitCode, outputLines);
            return (process.ExitCode, outputLines);
        }
    }
}
