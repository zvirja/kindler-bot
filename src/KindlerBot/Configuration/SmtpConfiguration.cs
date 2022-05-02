#nullable disable warnings

namespace KindlerBot.Configuration;

public class SmtpConfiguration
{
    public const string SectionName = "Smtp";

    public string FromEmail { get; set; }

    public string UserName { get; set; }

    public string Password { get; set; }

    public string RelayServer { get; set; }

    public int Port { get; set; }

    public string RelayServerEncryption { get; set; }
}