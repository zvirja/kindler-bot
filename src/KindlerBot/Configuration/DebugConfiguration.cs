#nullable disable warnings

namespace KindlerBot.Configuration;

public class DebugConfiguration
{
    public const string SectionName = "Debug";

    public bool KeepConversionTempWorkDir { get; set; }

    public bool LogHttpRequests { get; set; }
}
