using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.TerrainFeatures;

namespace GrassVariety;

public static class GrassManager
{
    internal const string LocationData_AllowedVarietyPrefix = $"{ModEntry.ModId}_AllowedVarietyPrefix";
    internal const string ModData_ChosenVariant = $"{ModEntry.ModId}_ChosenVariant";

    private static readonly FieldInfo Grass_whichWeed_Field = AccessTools.DeclaredField(typeof(Grass), "whichWeed");

    private static readonly PerScreen<List<GrassVarietyData>[]> grassVarietiesForCurrentLocation =
        new(AssetManager.InitGrassVarieties);

    internal static void Register(IModHelper helper)
    {
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.Player.Warped += OnWarped;

        Harmony harmony = new(ModEntry.ModId);
        try
        {
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Grass), "createDestroySprites"),
                prefix: new HarmonyMethod(typeof(GrassManager), nameof(Grass_createDestroySprites_Prefix))
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Grass), nameof(Grass.textureName)),
                postfix: new HarmonyMethod(typeof(GrassManager), nameof(Grass_textureName_Postfix))
            );
        }
        catch (Exception ex)
        {
            ModEntry.Log($"Failed to patch GrassManager, some visuals may be incorrect.\n{ex}", LogLevel.Warn);
        }
    }

    private static bool Grass_createDestroySprites_Prefix(Grass __instance, GameLocation location, Vector2 tileLocation)
    {
        if (location == null)
            return true;
        if (
            TryGetChosenGrassVariety(__instance, out GrassVarietyData? chosen)
            && chosen.DestroyColors is List<GrassDestroyColor?> destroyColors
        )
        {
            byte grassType = __instance.grassType.Value;
            if (grassType < 1 || grassType > destroyColors.Count)
                return true;
            if (destroyColors[grassType - 1] is not GrassDestroyColor destroyColor)
                return true;
            Game1.Multiplayer.broadcastSprites(
                location,
                new TemporaryAnimatedSprite(
                    28,
                    tileLocation * 64f + new Vector2(Game1.random.Next(-16, 16), Game1.random.Next(-16, 16)),
                    destroyColor.GetForSeason(location.GetSeason()),
                    8,
                    Game1.random.NextBool(),
                    Game1.random.Next(60, 100)
                )
            );
            return false;
        }
        return true;
    }

    private static void Grass_textureName_Postfix(Grass __instance, ref string __result)
    {
        if (__instance.texture.IsValueCreated && __instance.texture.Value.Name is string txName)
        {
            __result = txName;
        }
    }

    private static void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (e.OldLocation != null)
        {
            e.OldLocation.terrainFeatures.OnValueAdded -= OnNewGrassAdded;
            e.OldLocation.terrainFeatures.OnValueTargetUpdated -= OnGrassChanged;
        }
        SetupGrassVarietyForLocation(e.NewLocation);
    }

    private static void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        SetupGrassVarietyForLocation(Game1.currentLocation);
    }

    internal static bool TryGetLocationalProperty(
        GameLocation location,
        string propKey,
        [NotNullWhen(true)] out string? prop
    )
    {
        prop = null;
        if (location == null)
            return false;
        if (location.GetData()?.CustomFields?.TryGetValue(propKey, out prop) ?? false)
        {
            return !string.IsNullOrWhiteSpace(prop);
        }
        if (location.Map != null && location.Map.Properties != null && location.TryGetMapProperty(propKey, out prop))
        {
            return !string.IsNullOrWhiteSpace(prop);
        }
        if (location.GetLocationContext()?.CustomFields?.TryGetValue(propKey, out prop) ?? false)
        {
            return !string.IsNullOrWhiteSpace(prop);
        }
        return false;
    }

    private static void SetupGrassVarietyForLocation(GameLocation location)
    {
        if (location == null || location.map == null)
        {
            return;
        }

        List<GrassVarietyData>[] gvfcl = grassVarietiesForCurrentLocation.Value;
        gvfcl.ClearGrassVarieties();

        if (!TryGetLocationalProperty(location, LocationData_AllowedVarietyPrefix, out string? allowedVarietyPrefix))
        {
            allowedVarietyPrefix = null;
        }

        byte grassType = 1;
        GameStateQueryContext ctx = new(location, Game1.player, null, null, null);
        foreach (List<GrassVarietyData> varieties in AssetManager.GrassVarieties)
        {
            List<GrassVarietyData> grassList = gvfcl[grassType - 1];
            foreach (GrassVarietyData variety in varieties)
            {
                if (allowedVarietyPrefix == null)
                {
                    if (variety.ByLocationAllowanceOnly)
                        continue;
                }
                else
                {
                    if (!variety.Id.StartsWith(allowedVarietyPrefix))
                        continue;
                }

                if (GameStateQuery.CheckConditions(variety.Condition, ctx))
                {
                    for (int i = 0; i < variety.Weight; i++)
                    {
                        grassList.Add(variety);
                    }
                }
            }
            if (grassList.Count > 0)
                ModEntry.LogDebug(
                    $"Got {grassList.Count} varieties for grass type {grassType} in {location.NameOrUniqueName}"
                );
            grassType++;
        }

        foreach (TerrainFeature feature in location.terrainFeatures.Values)
        {
            if (feature is not Grass grass)
                continue;

            ApplyGrassVariety(gvfcl, grass);
        }

        location.terrainFeatures.OnValueAdded += OnNewGrassAdded;
        location.terrainFeatures.OnValueTargetUpdated += OnGrassChanged;
    }

    private static void ApplyGrassVariety(List<GrassVarietyData>[] gvfcl, Grass grass, bool newPlacement = false)
    {
        Random random = Utility.CreateDaySaveRandom(grass.Tile.X * 1000, grass.Tile.Y * 2000);
        if (!TryGetChosenGrassVariety(grass, out GrassVarietyData? chosen))
        {
            byte grassType = grass.grassType.Value;
            if (grassType < 1 || grassType > gvfcl.Length)
                return;

            List<GrassVarietyData> grassList = gvfcl[grassType - 1];
            if (grassList.Count == 0)
                return;

            chosen = random.ChooseFrom(grassList);
        }

        if (chosen == null || chosen.Id == AssetManager.DEFAULT)
            return;

        grass.texture = new Lazy<Texture2D>(chosen.LoadTexture);
        if (chosen.SubVariants != null && chosen.SubVariants.Count > 0)
        {
            if (newPlacement)
                grass.setUpRandom();
            int[] whichWeed = new int[4];
            for (int i = 0; i < 4; i++)
            {
                whichWeed[i] = random.ChooseFrom(chosen.SubVariants);
            }
            Grass_whichWeed_Field.SetValue(grass, whichWeed);
        }
        grass.modData[ModData_ChosenVariant] = chosen.Id;
    }

    private static bool TryGetChosenGrassVariety(Grass grass, [NotNullWhen(true)] out GrassVarietyData? chosen)
    {
        chosen = null;
        if (
            grass.modData.TryGetValue(ModData_ChosenVariant, out string chosenId)
            && AssetManager.RawGrassVarieties.TryGetValue(chosenId, out chosen)
        )
        {
            return chosen != null;
        }
        return false;
    }

    private static void OnNewGrassAdded(Vector2 key, TerrainFeature value)
    {
        if (value is Grass grass)
        {
            ApplyGrassVariety(grassVarietiesForCurrentLocation.Value, grass, newPlacement: true);
        }
    }

    private static void OnGrassChanged(Vector2 key, TerrainFeature old_target_value, TerrainFeature new_target_value)
    {
        if (new_target_value is Grass grass)
        {
            ApplyGrassVariety(grassVarietiesForCurrentLocation.Value, grass);
        }
    }
}
