using System.Diagnostics;
using GrassVariety.Integration;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.TerrainFeatures;

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
    internal static IGameContentHelper content = null!;
    internal static ModConfig Config = null!;

    public override void Entry(IModHelper helper)
    {
        Config = helper.ReadConfig<ModConfig>();
        mon = Monitor;
        content = helper.GameContent;

        GrassManager.Register(helper);
        AssetManager.Register(helper);

        helper.ConsoleCommands.Add(
            "gv-default_grass_weight",
            "Set the weight for the vanilla grass variant (0 by default)",
            ConsoleSetDefaultGrassWeight
        );
        helper.ConsoleCommands.Add("gv-grassify", "Put grass everywhere.", ConsoleGrassify);

        MoreGrassShim.Register(helper);
    }

    internal static IAssetName ParseAssetName(string asset)
    {
        return content.ParseAssetName(asset);
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

    private void ConsoleGrassify(string cmd, string[] args)
    {
        if (!Context.IsWorldReady)
            return;
        List<int> grassType = [];
        foreach (string arg in args)
        {
            if (int.TryParse(arg, out int grsType))
            {
                grassType.Add(grsType);
            }
        }
        if (grassType.Count == 0)
        {
            grassType.Add(Grass.springGrass);
        }
        xTile.Layers.Layer layer = Game1.currentLocation.Map.RequireLayer("Back");
        for (int x = 0; x < layer.LayerWidth; x++)
        {
            for (int y = 0; y < layer.LayerHeight; y++)
            {
                Vector2 pos = new(x, y);
                if (layer.Tiles[x, y] is null || Game1.currentLocation.terrainFeatures.ContainsKey(pos))
                    continue;
                Game1.currentLocation.terrainFeatures.Add(pos, new Grass(Random.Shared.ChooseFrom(grassType), 4));
            }
        }
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
