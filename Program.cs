using System.Diagnostics;
using SalesDemos.QuickLinks;

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "--help";

switch (command)
{
    case "--setup":
        await RunSetup();
        break;
    case "--launch":
        if (args.Length < 2) { Console.Error.WriteLine("Usage: --launch MACHINE-NAME"); return 1; }
        return await LaunchMachine(args[1]);
    case "--refresh":
        await RefreshShortcuts();
        break;
    default:
        PrintUsage();
        break;
}
return 0;

static async Task RunSetup()
{
    Console.WriteLine("=== SalesDemos QuickLinks Setup ===\n");

    using var client = new ScreenConnectClient();
    var config = AppConfig.Load();

    if (config.AuthCookie is not null)
    {
        Console.Write("Existing auth found. Validating... ");
        client.LoadPersistedAuth(config);
        if (await client.ValidateSessionAsync())
        {
            Console.WriteLine("still valid!");
            Console.Write("Re-create desktop shortcuts? (y/n): ");
            if (Console.ReadLine()?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true)
                CreateShortcuts();
            return;
        }
        Console.WriteLine("expired. Re-authenticating.\n");
    }

    Console.Write("Username: ");
    var username = Console.ReadLine()?.Trim() ?? "";

    Console.Write("Password: ");
    var password = ReadPassword();
    Console.WriteLine();

    var result = await client.LoginAsync(username, password);

    if (result.NeedsOtp)
    {
        Console.Write("MFA Code: ");
        var otp = Console.ReadLine()?.Trim() ?? "";
        result = await client.LoginAsync(username, password, otp);
    }

    if (!result.Success)
    {
        Console.Error.WriteLine($"Login failed: {result.ErrorMessage}");
        return;
    }

    config.AuthCookie = result.AuthCookie;
    config.AntiForgeryToken = null;
    config.CookieExpiry = result.CookieExpiry;
    config.Save();

    Console.WriteLine($"\nAuthenticated. Cookie valid until {result.CookieExpiry:yyyy-MM-dd}.");
    CreateShortcuts();
}

static async Task<int> LaunchMachine(string machineName)
{
    var machine = AppConfig.Machines.FirstOrDefault(m =>
        m.Name.Equals(machineName, StringComparison.OrdinalIgnoreCase));

    if (machine is null)
    {
        Console.Error.WriteLine($"Unknown machine: {machineName}");
        Console.Error.WriteLine($"Available: {string.Join(", ", AppConfig.Machines.Select(m => m.Name))}");
        return 1;
    }

    var config = AppConfig.Load();
    if (config.AuthCookie is null)
    {
        Console.Error.WriteLine("Not set up yet. Run with --setup first.");
        return 1;
    }

    using var client = new ScreenConnectClient();
    client.LoadPersistedAuth(config);

    if (!await client.ValidateSessionAsync())
    {
        Console.Error.WriteLine("Auth expired. Run --setup again to re-authenticate.");
        return 1;
    }

    var accessToken = await client.GetAccessTokenAsync(machine.SessionId);
    if (accessToken is null)
    {
        Console.Error.WriteLine("Failed to get access token. Auth may be expired — run --setup.");
        return 1;
    }

    var relayUrl = ScreenConnectClient.BuildRelayUrl(machine.SessionId, accessToken);
    config.Save();

    Process.Start(new ProcessStartInfo(relayUrl) { UseShellExecute = true });
    return 0;
}

static async Task RefreshShortcuts()
{
    var config = AppConfig.Load();
    if (config.AuthCookie is null)
    {
        Console.Error.WriteLine("Not set up yet. Run with --setup first.");
        return;
    }

    using var client = new ScreenConnectClient();
    client.LoadPersistedAuth(config);

    if (!await client.ValidateSessionAsync())
    {
        Console.Error.WriteLine("Auth expired. Run --setup again.");
        return;
    }

    CreateShortcuts();
    Console.WriteLine("Shortcuts refreshed.");
}

static void CreateShortcuts()
{
    var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName
        ?? throw new InvalidOperationException("Cannot determine exe path.");

    ShortcutManager.CreateAllShortcuts(exePath);
    Console.WriteLine($"\nCreated {AppConfig.Machines.Length} shortcuts on Desktop:");
    foreach (var m in AppConfig.Machines)
        Console.WriteLine($"  {m.Name}");
}

static string ReadPassword()
{
    var password = new System.Text.StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Backspace && password.Length > 0)
        {
            password.Length--;
            Console.Write("\b \b");
        }
        else if (!char.IsControl(key.KeyChar))
        {
            password.Append(key.KeyChar);
            Console.Write('*');
        }
    }
    return password.ToString();
}

static void PrintUsage()
{
    Console.WriteLine("""
        SalesDemos QuickLinks - ScreenConnect Desktop Shortcuts

        Usage:
          --setup           Authenticate and create desktop shortcuts
          --launch NAME     Connect to a machine (used by shortcuts)
          --refresh         Re-create shortcuts (if exe moved, etc.)

        Machines:
        """);
    foreach (var m in AppConfig.Machines)
        Console.WriteLine($"    {m.Name,-20} {m.SessionId}");
}
