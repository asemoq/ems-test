namespace Gs1Marking.Client.Options;

/// <summary>
/// Конфигурация клиента.
/// </summary>
public sealed class ClientOptions
{
    public const string SectionName = "Client";

    /// <summary>Имя хоста или IP-адрес сервера.</summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>TCP-порт сервера.</summary>
    public int Port { get; set; } = 5000;

    /// <summary>Файл для дописывания полученных кодов.</summary>
    public string OutputPath { get; set; } = "marking-codes.txt";

    /// <summary>Задержка между попытками переподключения в миллисекундах.</summary>
    public int ReconnectDelayMs { get; set; } = 2000;

    public TimeSpan ReconnectDelay => TimeSpan.FromMilliseconds(ReconnectDelayMs);
}
