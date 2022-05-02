using System.Threading.Tasks;

namespace KindlerBot.Conversion;

internal enum CalibreCliApp
{
    Meta,
    Convert,
    Smtp
}

internal interface ICalibreCliExec
{
    Task<(int exitCode, string[] output)> RunCalibre(CalibreCliApp app, string[] args);
}