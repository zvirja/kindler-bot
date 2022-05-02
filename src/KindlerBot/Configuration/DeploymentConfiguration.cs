#nullable disable warnings

using System;

namespace KindlerBot.Configuration;

public class DeploymentConfiguration
{
    public const string SectionName = "Deployment";

    public Uri PublicUrl { get; set; }

    public string ConfigStore { get; set; }
}