#nullable disable warnings

namespace KindlerBot.Configuration;

public class ConversionConfiguration
{
    public const string SectionName = "Conversion";

    public bool KeepTempWorkDir { get; set; }
}