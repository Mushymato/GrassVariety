using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace GrassVariety;

public sealed class GrassVarietyData
{
    public string Id { get; set; } = null!;
    public string Texture { get; set; } = null!;
    public string? Condition { get; set; } = null;
    public bool ByLocationAllowanceOnly { get; set; } = false;
    public int Weight { get; set; } = 1;
    public List<byte>? ApplyTo { get; set; } = null;

    internal Texture2D LoadTexture() => Game1.content.Load<Texture2D>(Texture);
}

public static class AssetManager
{
    internal const string Asset_GrassVariety = $"{ModEntry.ModId}/Data";
    internal const string DEFAULT = "DEFAULT";
    private const string DefaultGrassTexture = "TerrainFeatures\\grass";

    internal static List<GrassVarietyData>[] InitGrassVarieties() =>
        [
            [], // Grass.springGrass
            [], // Grass.caveGrass
            [], // Grass.frostGrass
            [], // Grass.lavaGrass
            [], // Grass.caveGrass2
            [], // Grass.cobweb
            [], // Grass.blueGrass
        ];

    internal static void ClearGrassVarieties(this List<GrassVarietyData>[] grassVarietiesArray)
    {
        foreach (List<GrassVarietyData> grassList in grassVarietiesArray)
        {
            grassList.Clear();
        }
    }

    private static Dictionary<string, GrassVarietyData>? rawGrassVarieties = null;
    private static readonly List<GrassVarietyData>[] grassVarieties = InitGrassVarieties();

    internal static List<GrassVarietyData>[] GrassVarieties
    {
        get
        {
            if (rawGrassVarieties != null)
            {
                return grassVarieties;
            }

            rawGrassVarieties = Game1.content.Load<Dictionary<string, GrassVarietyData>>(Asset_GrassVariety);
            foreach ((string key, GrassVarietyData variety) in rawGrassVarieties)
            {
                variety.Id = key;
                if (string.IsNullOrEmpty(variety.Texture) || !Game1.content.DoesAssetExist<Texture2D>(variety.Texture))
                    continue;
                if (variety.ApplyTo == null || !variety.ApplyTo.Any())
                {
                    variety.ApplyTo = [Grass.springGrass];
                }
                foreach (byte apply in variety.ApplyTo)
                {
                    if (apply >= 1 && apply <= grassVarieties.Length)
                        grassVarieties[apply - 1].Add(variety);
                    else
                        ModEntry.LogOnce(
                            $"GrassVariety '{variety.Id}' has invalid ApplyTo value '{apply}'",
                            LogLevel.Warn
                        );
                }
            }

            return grassVarieties;
        }
    }

    internal static void Register(IModHelper helper)
    {
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.Content.AssetsInvalidated += OnAssetsInvalidated;
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(Asset_GrassVariety))
            e.LoadFrom(LoadInitialGrass, AssetLoadPriority.Exclusive);
    }

    private static Dictionary<string, GrassVarietyData> LoadInitialGrass()
    {
        return new()
        {
            [DEFAULT] = new GrassVarietyData()
            {
                Id = DEFAULT,
                Texture = DefaultGrassTexture,
                Weight = ModEntry.Config.DefaultGrassWeight,
                ApplyTo =
                [
                    Grass.springGrass,
                    Grass.caveGrass,
                    Grass.frostGrass,
                    Grass.lavaGrass,
                    Grass.caveGrass2,
                    Grass.cobweb,
                    Grass.blueGrass,
                ],
            },
        };
    }

    private static void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo(Asset_GrassVariety)))
        {
            rawGrassVarieties = null;
            grassVarieties.ClearGrassVarieties();
        }
    }
}
