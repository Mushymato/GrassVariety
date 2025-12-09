using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData;
using StardewValley.Internal;
using StardewValley.TerrainFeatures;

namespace GrassVariety;

public sealed record GrassDestroyColor(Color ClrSpring, Color ClrSummer, Color ClrFall, Color ClrWinter)
{
    public static implicit operator GrassDestroyColor(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null!;
        return StringArrayToGrassDestroyColor(value.Split(','));
    }

    public static implicit operator GrassDestroyColor(string[]? value)
    {
        if (value == null)
            return null!;
        return StringArrayToGrassDestroyColor(value);
    }

    private static GrassDestroyColor StringArrayToGrassDestroyColor(string[] args)
    {
        if (!ArgUtility.TryGet(args, 0, out string clrSpringStr, out _, allowBlank: false, name: "string clrSpringStr"))
        {
            throw new InvalidCastException("Cannot convert empty array to GrassDestroyColor");
        }
        if (Utility.StringToColor(clrSpringStr) is not Color clrSpring)
        {
            throw new InvalidCastException("Must have at least one color");
        }
        return new(
            clrSpring,
            (
                ArgUtility.TryGet(
                    args,
                    1,
                    out string clrSummerStr,
                    out _,
                    allowBlank: false,
                    name: "string clrSummerStr"
                )
                    ? Utility.StringToColor(clrSummerStr)
                    : clrSpring
            ) ?? clrSpring,
            (
                ArgUtility.TryGet(args, 2, out string clrFallStr, out _, allowBlank: false, name: "string clrFallStr")
                    ? Utility.StringToColor(clrFallStr)
                    : clrSpring
            ) ?? clrSpring,
            (
                ArgUtility.TryGet(
                    args,
                    3,
                    out string clrWinterStr,
                    out _,
                    allowBlank: false,
                    name: "string clrWinterStr"
                )
                    ? Utility.StringToColor(clrWinterStr)
                    : clrSpring
            ) ?? clrSpring
        );
    }

    internal Color GetForSeason(Season season)
    {
        return season switch
        {
            Season.Summer => ClrSummer,
            Season.Fall => ClrFall,
            Season.Winter => ClrWinter,
            _ => ClrSpring,
        };
    }
}

public sealed class GrassOnCutItemSpawnData : GenericSpawnItemDataWithCondition
{
    public ItemQuerySearchMode SearchMode { get; set; } = ItemQuerySearchMode.AllOfTypeItem;
    public bool RequiresScythe { get; set; } = false;
}

public sealed class GrassVarietyData
{
    public string Id { get; set; } = AssetManager.DEFAULT;

    public string Texture { get; set; } = AssetManager.DefaultGrassTexture;

    public string? Condition { get; set; } = null;

    public bool ByLocationAllowanceOnly { get; set; } = false;

    public int Weight { get; set; } = 1;

    public int PersistDays { get; set; } = 1;

    [JsonConverter(typeof(StringIntListConverter))]
    public List<int>? SubVariants { get; set; } = null;

    [JsonConverter(typeof(GrassIndexSetConverter))]
    public HashSet<byte>? ApplyTo { get; set; } = null;

    [JsonConverter(typeof(GrassDestroyColorListConverter))]
    public List<GrassDestroyColor?>? DestroyColors { get; set; } = null;

    public string? InheritOnCutFrom { get; set; } = AssetManager.DEFAULT;

    public List<GrassOnCutItemSpawnData>? OnCutItemSpawns { get; set; } = null;

    public List<string>? OnCutTileActions { get; set; } = null;

    public bool EnableAtlasOptimization { get; set; } = true;

    internal PostionOnComp[]? PosOnCompArray { get; set; } = null;

    internal PostionOnComp? GetPosOnComp(byte applyTo) =>
        (
            EnableAtlasOptimization
            && PosOnCompArray != null
            && applyTo >= Grass.springGrass
            && applyTo <= Grass.blueGrass
        )
            ? PosOnCompArray[applyTo - 1]
            : null;

    internal Texture2D LoadTexture(byte applyTo) =>
        GetPosOnComp(applyTo) is PostionOnComp posOnComp
            ? GrassComp.LoadTexture(posOnComp.Sheet)
            : Game1.content.Load<Texture2D>(Texture);
}

public static class AssetManager
{
    internal const string Asset_GrassVariety = $"{ModEntry.ModId}/Data";
    internal const string DEFAULT = "DEFAULT";
    internal const string DefaultGrassTexture = "TerrainFeatures\\grass";

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

    private static Dictionary<string, GrassVarietyData>? rawGrassVarieties = null;
    internal static Dictionary<string, GrassVarietyData> RawGrassVarieties =>
        rawGrassVarieties ??= Game1.content.Load<Dictionary<string, GrassVarietyData>>(Asset_GrassVariety);
    private static readonly List<GrassVarietyData>[] grassVarieties = InitGrassVarieties();

    private static bool grassVarietiesLoaded = false;
    internal static List<GrassVarietyData>[] GrassVarieties
    {
        get
        {
            if (rawGrassVarieties != null && grassVarietiesLoaded)
            {
                return grassVarieties;
            }

            foreach ((string key, GrassVarietyData variety) in RawGrassVarieties)
            {
                variety.Id = key;
                if (string.IsNullOrEmpty(variety.Texture) || !Game1.content.DoesAssetExist<Texture2D>(variety.Texture))
                    continue;
                if (variety.ApplyTo == null || variety.ApplyTo.Count == 0)
                {
                    variety.ApplyTo = [Grass.springGrass];
                }
                if (
                    variety.InheritOnCutFrom != null
                    && RawGrassVarieties.TryGetValue(variety.InheritOnCutFrom, out GrassVarietyData? inheritFrom)
                    && inheritFrom.InheritOnCutFrom == null
                )
                {
                    variety.OnCutItemSpawns ??= inheritFrom.OnCutItemSpawns;
                    variety.OnCutTileActions ??= inheritFrom.OnCutTileActions;
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
            grassVarietiesLoaded = true;

            GrassComp.RecombineSpriteSheet();

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
                InheritOnCutFrom = null,
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
            grassVarietiesLoaded = false;
            foreach (List<GrassVarietyData> grassList in grassVarieties)
            {
                grassList.Clear();
            }
            GrassComp.InvalidateData();
        }
        else
        {
            GrassComp.CheckInvalidate(e.NamesWithoutLocale);
        }
    }
}
