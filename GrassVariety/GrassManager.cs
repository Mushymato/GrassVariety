using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.GameData.Objects;
using StardewValley.Internal;
using StardewValley.TerrainFeatures;

namespace GrassVariety;

public static class GrassManager
{
    internal const string ModData_ChosenVariant = $"{ModEntry.ModId}_ChosenVariant";
    internal const string ModData_ForcedVariant = $"{ModEntry.ModId}_ForcedVariant";
    internal const string ModData_NextRecheck = $"{ModEntry.ModId}_NextRecheck";
    internal const string CustomFields_GrassStarterKind = $"{ModEntry.ModId}/GrassStarterKind";
    internal const string CustomFields_GrassStarterVariety = $"{ModEntry.ModId}/GrassStarterVariety";
    internal const string CustomFields_GrassStarterPlacementSound = $"{ModEntry.ModId}/GrassStarterPlacementSound";

    private static Random GetTileRand(Vector2 xy) => Utility.CreateDaySaveRandom(xy.X * 1000, xy.Y * 2000);

    private static readonly ConditionalWeakTable<GameLocation, LocationGrassWatcher?> grassWatchers = [];

    internal static void Register(IModHelper helper)
    {
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.Player.Warped += OnWarped;
        helper.Events.GameLoop.Saving += OnSaving;
        helper.Events.GameLoop.Saved += OnSaved;

        Harmony harmony = new(ModEntry.ModId);

        try
        {
            harmony.Patch(
                original: AccessTools.DeclaredMethod(
                    typeof(StardewValley.Object),
                    nameof(StardewValley.Object.placementAction)
                ),
                prefix: new HarmonyMethod(typeof(GrassManager), nameof(SObject_placementAction_Prefix))
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.growWeedGrass)),
                transpiler: new HarmonyMethod(typeof(GrassManager), nameof(GameLocation_growWeedGrass_Transpiler))
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Grass), nameof(Grass.setUpRandom)),
                postfix: new HarmonyMethod(typeof(GrassManager), nameof(Grass_setUpRandom_Postfix))
            );
        }
        catch (Exception ex)
        {
            ModEntry.Log($"Failed to patch GrassVariety (custom grass starter):\n{ex}", LogLevel.Error);
        }

        try
        {
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Grass), nameof(Grass.TryDropItemsOnCut)),
                postfix: new HarmonyMethod(typeof(GrassManager), nameof(Grass_TryDropItemsOnCut_Postfix))
            );
        }
        catch (Exception ex)
        {
            ModEntry.Log($"Failed to patch GrassVariety (drop item):\n{ex}", LogLevel.Error);
        }

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
            ModEntry.Log(
                $"Failed to patch GrassVariety (visuals), some visuals may be incorrect.\n{ex}",
                LogLevel.Warn
            );
        }
    }

    private static void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        grassWatchers.GetValue(Game1.currentLocation, LocationGrassWatcher.Create)?.Activate();
    }

    private static void OnSaving(object? sender, SavingEventArgs e)
    {
        foreach ((_, LocationGrassWatcher? watcher) in grassWatchers)
        {
            watcher?.Deactivate();
        }
    }

    private static void OnSaved(object? sender, SavedEventArgs e)
    {
        foreach ((_, LocationGrassWatcher? watcher) in grassWatchers)
        {
            watcher?.Activate();
        }
    }

    private static void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        foreach ((_, LocationGrassWatcher? watcher) in grassWatchers)
        {
            watcher?.Deactivate();
        }
        grassWatchers.Clear();
    }

    private static void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (grassWatchers.TryGetValue(e.OldLocation, out LocationGrassWatcher? oldWatcher))
        {
            oldWatcher?.Deactivate();
        }
        grassWatchers.GetValue(e.NewLocation, LocationGrassWatcher.Create)?.Activate();
    }

    private static Grass ModifySpreadGrass(Grass newGrass, Grass sourceGrass)
    {
        if (TryGetForcedGrassVariety(sourceGrass, out GrassVarietyData? chosen))
        {
            newGrass.modData[ModData_ForcedVariant] = chosen.Id;
        }
        return newGrass;
    }

    private static void Grass_setUpRandom_Postfix(Grass __instance, ref int[] ___whichWeed)
    {
        if (!TryGetChosenGrassVariety(__instance, out GrassVarietyData? chosen))
            return;
        PostionOnComp? posOnComp = chosen.GetPosOnComp(__instance.grassType.Value);
        int xOffset = posOnComp?.XBase ?? 0;
        int yOffset = posOnComp?.Y ?? 0;
        if (chosen.SubVariants != null && chosen.SubVariants.Count > 0)
        {
            Random random = GetTileRand(__instance.Tile);
            for (int i = 0; i < 4; i++)
            {
                ___whichWeed[i] = xOffset + random.ChooseFrom(chosen.SubVariants);
            }
        }
        else
        {
            for (int i = 0; i < 4; i++)
            {
                ___whichWeed[i] = xOffset + ___whichWeed[i];
            }
        }
        if (yOffset > 0)
        {
            int yOffsetValue = __instance.grassSourceOffset.Value % GrassComp.Y_HEIGHT;
            __instance.grassSourceOffset.Value = GrassComp.Y_HEIGHT * yOffset + yOffsetValue;
        }
    }

    private static IEnumerable<CodeInstruction> GameLocation_growWeedGrass_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);
            // IL_015e: ldloc.s 4
            // IL_0160: ldfld class Netcode.NetByte StardewValley.TerrainFeatures.Grass::grassType
            // IL_0165: callvirt instance !0 class Netcode.NetFieldBase`2<uint8, class Netcode.NetByte>::get_Value()
            // IL_016a: ldsfld class [System.Runtime]System.Random StardewValley.Game1::random
            // IL_016f: ldc.i4.1
            // IL_0170: ldc.i4.3
            // IL_0171: callvirt instance int32 [System.Runtime]System.Random::Next(int32, int32)
            // IL_0176: newobj instance void StardewValley.TerrainFeatures.Grass::.ctor(int32, int32)
            CodeMatch[] toMatchFor =
            [
                new(inst => inst.IsLdloc()),
                new(OpCodes.Ldfld, AccessTools.DeclaredField(typeof(Grass), nameof(Grass.grassType))),
                new(
                    OpCodes.Callvirt,
                    AccessTools.DeclaredPropertyGetter(typeof(Netcode.NetByte), nameof(Netcode.NetByte.Value))
                ),
                new(OpCodes.Ldsfld, AccessTools.DeclaredField(typeof(Game1), nameof(Game1.random))),
                new(OpCodes.Ldc_I4_1),
                new(OpCodes.Ldc_I4_3),
                new(OpCodes.Callvirt, AccessTools.DeclaredField(typeof(Random), nameof(Random.Next))),
                new(OpCodes.Newobj, AccessTools.DeclaredConstructor(typeof(Grass), [typeof(int), typeof(int)])),
            ];
            matcher
                .MatchStartForward(toMatchFor)
                .ThrowIfNotMatch("Failed to find 'new Grass(grass.grassType.Value, Game1.random.Next(1, 3))'");
            CodeInstruction sourceGrassLoc = new(matcher.Opcode, matcher.Operand);
            matcher.Advance(toMatchFor.Length);
            matcher.Insert([
                sourceGrassLoc,
                new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(GrassManager), nameof(ModifySpreadGrass))),
            ]);

            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in Building_draw_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static void Grass_TryDropItemsOnCut_Postfix(Grass __instance, Tool tool, ref bool __result)
    {
        if (!__result)
            return;
        if (!TryGetChosenGrassVariety(__instance, out GrassVarietyData? chosen))
            return;
        Vector2 tile = __instance.Tile;
        GameLocation location = __instance.Location;
        Farmer who = tool?.getLastFarmerToUse() ?? Game1.player;
        bool isScythe = tool?.isScythe() ?? false;
        if (chosen.OnCutItemSpawns != null)
        {
            GameStateQueryContext gqCtx = new(location, who, null, tool, null);
            ItemQueryContext iqCtx = new(location, who, null, $"{ModEntry.ModId}:{chosen.Id} OnCut");
            Vector2 tilePos = __instance.Tile * Game1.tileSize;
            foreach (GrassOnCutItemSpawnData iq in chosen.OnCutItemSpawns)
            {
                if ((iq.RequiresScythe && !isScythe) || !GameStateQuery.CheckConditions(iq.Condition, gqCtx))
                    continue;
                foreach (ItemQueryResult spawned in ItemQueryResolver.TryResolve(iq, iqCtx, iq.SearchMode))
                {
                    if (spawned.Item is Item item && item.Stack > 0)
                    {
                        Game1.createMultipleItemDebris(item, tilePos, -1, location);
                    }
                }
            }
        }
        if (chosen.OnCutTileActions != null)
        {
            xTile.Dimensions.Location loc = new((int)tile.X, (int)tile.Y);
            foreach (string tileAction in chosen.OnCutTileActions)
            {
                location.performAction(tileAction, who, loc);
            }
        }
    }

    private static bool SObject_placementAction_Prefix(
        StardewValley.Object __instance,
        GameLocation location,
        int x,
        int y,
        ref bool __result
    )
    {
        if (
            Game1.objectData.TryGetValue(__instance.ItemId, out ObjectData? data)
            && data.CustomFields is Dictionary<string, string> customFields
            && customFields.TryGetValue(CustomFields_GrassStarterKind, out string? grassKindStr)
            && GrassIndexHelper.GetGrassIndexFromString(grassKindStr) is byte grassKind
        )
        {
            Vector2 tile = new(x / 64, y / 64);
            if (location.objects.ContainsKey(tile) || location.terrainFeatures.ContainsKey(tile))
            {
                __result = false;
                return false;
            }
            Grass grass = new(grassKind, 4);
            if (
                customFields.TryGetValue(CustomFields_GrassStarterVariety, out string? grassStarterPrefix)
                && !string.IsNullOrEmpty(grassStarterPrefix)
            )
            {
                List<string> varietyIds = [];
                foreach (GrassVarietyData varietyData in AssetManager.GrassVarieties[grassKind - 1])
                {
                    if (varietyData.Id.StartsWith(grassStarterPrefix))
                    {
                        for (int i = 0; i < varietyData.Weight; i++)
                            varietyIds.Add(varietyData.Id);
                    }
                }
                if (varietyIds.Count > 0)
                    grass.modData[ModData_ForcedVariant] = GetTileRand(tile).ChooseFrom(varietyIds);
            }
            location.terrainFeatures.Add(tile, grass);
            if (!customFields.TryGetValue(CustomFields_GrassStarterPlacementSound, out string? placementSound))
                placementSound = "dirtyHit";
            location.playSound(placementSound);
            __result = true;
            return false;
        }
        return true;
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
        if (TryGetChosenGrassVariety(__instance, out GrassVarietyData? chosen))
        {
            __result = chosen.Texture;
        }
    }

    internal static void ChooseAndApplyGrassVariety(
        List<GrassVarietyData>[] gvfcl,
        Grass grass,
        bool newPlacement = false
    )
    {
        Random random = GetTileRand(grass.Tile);
        if (TryGetForcedGrassVariety(grass, out GrassVarietyData? chosen))
        {
            ApplyGrassVariety(grass, newPlacement, chosen, random);
            return;
        }

        byte grassType = grass.grassType.Value;
        if (grassType < 1 || grassType > gvfcl.Length)
            return;
        List<GrassVarietyData> grassList = gvfcl[grassType - 1];
        if (grassList.Count == 0)
        {
            UnapplyGrassVarietyState(grass);
            return;
        }

        if (!newPlacement)
        {
            if (
                !grass.modData.TryGetValue(ModData_NextRecheck, out string? nextRecheckDayStr)
                || (int.TryParse(nextRecheckDayStr, out int nextRecheckDay) && Game1.Date.TotalDays >= nextRecheckDay)
            )
            {
                UnapplyGrassVarietyState(grass);
            }
            else if (TryGetChosenGrassVariety(grass, out chosen) && !grassList.Contains(chosen))
            {
                chosen = null;
            }
        }

        if (chosen == null)
        {
            chosen = random.ChooseFrom(grassList);
            if (chosen.PersistDays == -1)
            {
                grass.modData[ModData_NextRecheck] = "-1";
            }
            else if (chosen.PersistDays > 0)
            {
                grass.modData[ModData_NextRecheck] = (Game1.Date.TotalDays + chosen.PersistDays).ToString();
            }
        }
        ApplyGrassVariety(grass, newPlacement, chosen, random);
    }

    private static void ApplyGrassVariety(Grass grass, bool newPlacement, GrassVarietyData chosen, Random random)
    {
        if (chosen == null)
            return;
        grass.modData[ModData_ChosenVariant] = chosen.Id;
        if (chosen.Id == AssetManager.DEFAULT)
            return;
        grass.texture = new Lazy<Texture2D>(() => chosen.LoadTexture(grass.grassType.Value));
        grass.setUpRandom();
    }

    private static bool TryGetForcedGrassVariety(Grass grass, [NotNullWhen(true)] out GrassVarietyData? chosen)
    {
        chosen = null;
        if (
            grass.modData.TryGetValue(ModData_ForcedVariant, out string chosenId)
            && AssetManager.RawGrassVarieties.TryGetValue(chosenId, out chosen)
            && (chosen.ApplyTo?.Contains(grass.grassType.Value) ?? false)
        )
        {
            return chosen != null;
        }
        grass.modData.Remove(ModData_ForcedVariant);
        return false;
    }

    private static bool TryGetChosenGrassVariety(Grass grass, [NotNullWhen(true)] out GrassVarietyData? chosen)
    {
        chosen = null;
        if (
            grass.modData.TryGetValue(ModData_ChosenVariant, out string chosenId)
            && AssetManager.RawGrassVarieties.TryGetValue(chosenId, out chosen)
            && (chosen.ApplyTo?.Contains(grass.grassType.Value) ?? false)
        )
        {
            return chosen != null;
        }
        UnapplyGrassVarietyState(grass);
        return false;
    }

    private static void UnapplyGrassVarietyState(Grass grass)
    {
        grass.grassSourceOffset.Value %= GrassComp.Y_HEIGHT;
        grass.modData.Remove(ModData_ChosenVariant);
        grass.texture = new Lazy<Texture2D>(() => Game1.content.Load<Texture2D>(AssetManager.DefaultGrassTexture));
    }
}
