using System.Diagnostics;
using StardewModdingAPI;

namespace GrassVariety;

public sealed class ModEntry : Mod
{
#if DEBUG
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Debug;
#else
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Trace;
#endif

    public const string ModId = "mushymato.GrassVariety";
    private static IMonitor? mon;
    internal static ModConfig Config = null!;

    public override void Entry(IModHelper helper)
    {
        Config = helper.ReadConfig<ModConfig>();
        mon = Monitor;

        GrassManager.Register(helper);
        AssetManager.Register(helper);

        helper.ConsoleCommands.Add(
            "gv-default_grass_weight",
            "Set the weight for the vanilla grass variant (0 by default)",
            ConsoleSetDefaultGrassWeight
        );
    }

    private void ConsoleSetDefaultGrassWeight(string cmd, string[] args)
    {
        if (Config == null)
            return;
        if (args.Length == 0)
            Config.DefaultGrassWeight = 0;
        else if (int.TryParse(args[0], out int weight))
            Config.DefaultGrassWeight = weight;
        Log($"Default grass weight set to {Config.DefaultGrassWeight}");
        Helper.GameContent.InvalidateCache(AssetManager.Asset_GrassVariety);
    }

    /// <summary>SMAPI static monitor Log wrapper</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    internal static void Log(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon!.Log(msg, level);
    }

    /// <summary>SMAPI static monitor LogOnce wrapper</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    internal static void LogOnce(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon!.LogOnce(msg, level);
    }

    /// <summary>SMAPI static monitor Log wrapper, debug only</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    [Conditional("DEBUG")]
    internal static void LogDebug(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon!.Log(msg, level);
    }
}
