using System.Text.Json;

namespace SalesDemos.QuickLinks;

public record MachineEntry(string Name, string SessionId);

public class AppConfig
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SalesDemos.QuickLinks");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    public string? AuthCookie { get; set; }
    public string? AntiForgeryToken { get; set; }
    public DateTime? CookieExpiry { get; set; }

    // ScreenConnect instance constants
    public const string BaseUrl = "https://schultztechnology.screenconnect.com";
    public const string RelayHost = "instance-fge7fa-relay.screenconnect.com";
    public const int RelayPort = 443;
    public const string UrlScheme = "sc-0a22c5ebaa67dc29";
    public const string SessionGroup = "All Machines";
    public const string RsaPublicKey = "BgIAAACkAABSU0ExAAgAAAEAAQCJShriOTU1n1QWjVxGC83mVCfVLNWsqEdy/n5LVifb7HhBLvUVUXLdRapSkEHOwz/kcdwTSnlXBKzQuLi1izLR7GtgXv2m+oZmGR3bvyENyqkE4o+SBoS8kkv7dcaaz6d0MAcHJMgm7tX0bPBlg46IIY7A4anfREabHKb16j9G/FtoroPNFmHa4I5rn7IBUFlSNOMGYN9bY4Rh3EP7lmYw8jXaacHhVUP3j6o3qnq+QHRx+xO/PMUKz17a2q+On0sILtXXXZWa3N+sH8rNFMc8GEVePt7yXlcfSC6++ke0JIkNjuVCPP6waqGWkVh0imACbLvlcYEhlFOkMgwMyA6i";

    public static readonly MachineEntry[] Machines =
    [
        new("DEMO-DAYSMART",  "01e8b395-e662-4327-9a07-5a0ed8b2591d"),
        new("DEMO-DIGITAIL",  "a94a0737-25d5-45e2-a023-ad0e2ffb1fc0"),
        new("DEMO-EZYVET",    "7c6fb5de-dc90-4ab3-bcc5-4747aca2cfeb"),
        new("DEMO-IDEXX-NEO", "1061d173-2214-4763-8504-5d7e42859f34"),
        new("DEMO-INSTINCT",  "a60065af-3f1f-4828-8bf0-76a8b6598933"),
        new("DEMO-PULSE",     "15384601-3a63-4a9e-98ca-61d48b094ef1"),
        new("DEMO-RHAPSODY",  "38ac75d4-761f-4b07-9b06-7fc47e72733f"),
        new("DEMO-SHEPHERD",  "4cf23865-3173-4704-8ece-b78027ce768a"),
        new("DEMO-VETPORT",   "897cfa4f-8e74-4480-b1a3-cec2317bbd6e"),
        new("DEMO-VETSPIRE",  "ef15958b-4501-4e31-81c0-e79d9901a923"),
        new("FETCHIT-DC",     "f6594072-e0f1-4b93-9787-db347c84a1a5"),
        new("FETCHIT-MDT",    "752bdce1-d9f7-4d26-8065-c17601348f76"),
    ];

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new AppConfig();

        var json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
