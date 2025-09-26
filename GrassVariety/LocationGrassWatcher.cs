using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.TerrainFeatures;

namespace GrassVariety;

internal class LocationGrassWatcher(GameLocation location) : IDisposable
{
    internal const string LocationData_AllowedVarietyPrefix = $"{ModEntry.ModId}_AllowedVarietyPrefix";

    private bool IsActive = false;
    private List<GrassVarietyData>[] grassVarietiesForLocation = AssetManager.InitGrassVarieties();

    internal static LocationGrassWatcher? Create(GameLocation location)
    {
        if (location == null || location.map == null)
        {
            return null;
        }
        return new LocationGrassWatcher(location);
    }

    private static bool TryGetLocationalProperty(
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

    internal void Activate()
    {
        if (IsActive)
            return;
        grassVarietiesForLocation.ClearGrassVarieties();
        if (!TryGetLocationalProperty(location, LocationData_AllowedVarietyPrefix, out string? allowedVarietyPrefix))
        {
            allowedVarietyPrefix = null;
        }
        if (allowedVarietyPrefix != null)
        {
            ModEntry.Log($"Grass allowed variety prefix for {location.NameOrUniqueName}: '{allowedVarietyPrefix}'");
        }

        byte grassType = 1;
        GameStateQueryContext ctx = new(location, Game1.player, null, null, null);
        foreach (List<GrassVarietyData> varieties in AssetManager.GrassVarieties)
        {
            List<GrassVarietyData> grassList = grassVarietiesForLocation[grassType - 1];
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
                ModEntry.LogOnce(
                    $"{grassList.Count} varieties for grass type {grassType} in {location.NameOrUniqueName}"
                );
            grassType++;
        }

        foreach (TerrainFeature feature in location.terrainFeatures.Values)
        {
            if (feature is not Grass grass)
                continue;

            GrassManager.ChooseAndApplyGrassVariety(grassVarietiesForLocation, grass);
        }

        location.terrainFeatures.OnValueAdded += OnNewGrassAdded;
        location.terrainFeatures.OnValueTargetUpdated += OnGrassChanged;
        IsActive = true;
    }

    internal void Deactivate()
    {
        if (!IsActive)
            return;
        location.terrainFeatures.OnValueAdded -= OnNewGrassAdded;
        location.terrainFeatures.OnValueTargetUpdated -= OnGrassChanged;
        IsActive = false;
    }

    private void OnNewGrassAdded(Vector2 key, TerrainFeature value)
    {
        if (value is Grass grass)
        {
            GrassManager.ChooseAndApplyGrassVariety(grassVarietiesForLocation, grass, newPlacement: true);
        }
    }

    private void OnGrassChanged(Vector2 key, TerrainFeature old_target_value, TerrainFeature new_target_value)
    {
        if (new_target_value is Grass grass)
        {
            GrassManager.ChooseAndApplyGrassVariety(grassVarietiesForLocation, grass);
        }
    }

    public void Dispose()
    {
        if (grassVarietiesForLocation == null)
            return;
        Deactivate();
        grassVarietiesForLocation = null!;
        GC.SuppressFinalize(this);
    }

    ~LocationGrassWatcher() => Dispose();
}
