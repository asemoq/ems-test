namespace Gs1Marking.Server.Options;

/// <summary>
/// Конфигурация транспорта сервера.
/// </summary>
public sealed class ServerOptions
{
    public const string SectionName = "Server";

    /// <summary>IP-адрес для привязки.</summary>
    public string BindAddress { get; set; } = "127.0.0.1";

    /// <summary>TCP-порт для прослушивания.</summary>
    public int Port { get; set; } = 5000;
}
