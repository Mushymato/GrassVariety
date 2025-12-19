using System.Buffers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.TerrainFeatures;

namespace GrassVariety.Integration;

internal sealed class MoreGrassPackContext(
    IContentPack contentPack,
    Texture2D spriteSheet,
    GrassVarietyData grassVarietyData
)
{
    private static bool GetFlagFromManifest(IContentPack contentPack, string key, bool defaultValue)
    {
        if (contentPack.Manifest.ExtraFields.TryGetValue(key, out object? isBoolV) && isBoolV is bool v)
        {
            return v;
        }
        return defaultValue;
    }

    private static long GetIntFromManifest(IContentPack contentPack, string key, int defaultValue)
    {
        if (contentPack.Manifest.ExtraFields.TryGetValue(key, out object? intVal) && intVal is long v)
        {
            return v;
        }
        return defaultValue;
    }

    internal static MoreGrassPackContext? Make(IContentPack contentPack)
    {
        bool isGreenGrass = GetFlagFromManifest(contentPack, $"{ModEntry.ModId}/IsGreenGrass", true);
        bool isBlueGrass = GetFlagFromManifest(contentPack, $"{ModEntry.ModId}/IsBlueGrass", false);
        long weight = GetIntFromManifest(contentPack, $"{ModEntry.ModId}/Weight", 1);
        if (!isGreenGrass && !isBlueGrass)
            return null;

        List<List<Texture2D>> sourceTextureList =
        [
            [],
            [],
            [],
            [],
        ];
        foreach (Season season in Enum.GetValues<Season>())
        {
            string seasonDir = Path.Combine(contentPack.DirectoryPath, season.ToString().ToLowerInvariant());
            foreach (string file in Directory.GetFiles(seasonDir))
            {
                if (!file.EndsWith(".png"))
                    continue;
                string relFile = Path.GetRelativePath(contentPack.DirectoryPath, file);
                Texture2D srcTx = contentPack.ModContent.Load<Texture2D>(relFile);
                sourceTextureList[(int)season].Add(srcTx);
            }
        }
        int count = sourceTextureList.Max(lst => lst.Count);
        if (count == 0)
        {
            ModEntry.Log($"No grass sprites found in '{contentPack.Manifest.UniqueID}'");
            return null;
        }

        int height = GrassComp.SPRITE_HEIGHT * (isBlueGrass ? 12 : 5);
        int width = GrassComp.SPRITE_WIDTH * count;

        Texture2D moreGrassSpriteSheet = new(Game1.graphics.GraphicsDevice, width, height);
        Color[] targetData = ArrayPool<Color>.Shared.Rent(moreGrassSpriteSheet.GetElementCount());
        Array.Fill(targetData, Color.Transparent);

        int xOffset = 0;

        List<string> includeSeason = [];

        Rectangle sourceRect = new(0, 0, GrassComp.SPRITE_WIDTH, GrassComp.SPRITE_HEIGHT);

        for (int i = 0; i < sourceTextureList.Count; i++)
        {
            List<int> yOffsets = [];
            if (isGreenGrass)
            {
                int yOffset = GrassComp.SPRITE_HEIGHT * i;
                if (i == 3)
                    yOffset += GrassComp.SPRITE_HEIGHT;
                yOffsets.Add(yOffset);
            }
            if (isBlueGrass)
            {
                yOffsets.Add(GrassComp.SPRITE_HEIGHT * (i + 8));
            }
            xOffset = 0;

            List<Texture2D> srcList = sourceTextureList[i];
            if (srcList.Count > 0)
            {
                includeSeason.Add(((Season)i).ToString().ToLowerInvariant());
            }
            else
            {
                continue;
            }
            int j = 0;
            while (xOffset < width)
            {
                Texture2D sourceTexture = srcList[j];

                Color[] sourceData = ArrayPool<Color>.Shared.Rent(sourceTexture.GetElementCount());
                sourceTexture.GetData(sourceData, 0, sourceTexture.GetElementCount());
                foreach (int yOffset in yOffsets)
                {
                    GrassComp.CopySourceSpriteToTarget(
                        ref sourceData,
                        sourceTexture.Width,
                        sourceRect,
                        ref targetData,
                        moreGrassSpriteSheet.Width,
                        new Rectangle(
                            xOffset,
                            yOffset
                                + (
                                    sourceTexture.Height < GrassComp.SPRITE_HEIGHT
                                        ? GrassComp.SPRITE_HEIGHT - sourceTexture.Height
                                        : 0
                                ),
                            GrassComp.SPRITE_WIDTH,
                            GrassComp.SPRITE_HEIGHT
                        )
                    );
                }
                ArrayPool<Color>.Shared.Return(sourceData);

                xOffset += GrassComp.SPRITE_WIDTH;
                j++;
                if (j >= srcList.Count)
                {
                    j = 0;
                }
            }
        }

        moreGrassSpriteSheet.SetData(targetData, 0, moreGrassSpriteSheet.GetElementCount());
        ArrayPool<Color>.Shared.Return(targetData);

        GrassVarietyData grassVarietyData = new()
        {
            Id = $"{ModEntry.ModId}@{contentPack.Manifest.UniqueID}@MoreGrassShim",
            Texture = $"{ModEntry.ModId}/{contentPack.Manifest.UniqueID}/MoreGrassSheet",
            Weight = (int)weight,
            SubVariants = Enumerable.Range(0, count).ToList(),
            ApplyTo = [],
        };
        if (isGreenGrass)
        {
            grassVarietyData.ApplyTo.Add(Grass.springGrass);
        }
        if (isBlueGrass)
        {
            grassVarietyData.ApplyTo.Add(Grass.blueGrass);
        }
        if (includeSeason.Count < 4)
        {
            grassVarietyData.Condition = string.Concat("LOCATION_SEASON Here ", string.Join(' ', includeSeason));
        }
        moreGrassSpriteSheet.Name = grassVarietyData.Texture;
        return new MoreGrassPackContext(contentPack, moreGrassSpriteSheet, grassVarietyData);
    }

    internal void AssetRequested(AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(grassVarietyData.Texture))
        {
            e.LoadFrom(() => spriteSheet, AssetLoadPriority.Low, onBehalfOf: contentPack.Manifest.UniqueID);
        }
        if (e.NameWithoutLocale.IsEquivalentTo(AssetManager.Asset_GrassVariety))
        {
            e.Edit(
                (asset) =>
                {
                    IDictionary<string, GrassVarietyData> gvAssetdata = asset
                        .AsDictionary<string, GrassVarietyData>()
                        .Data;
                    gvAssetdata[grassVarietyData.Id] = grassVarietyData;
                },
                AssetEditPriority.Early
            );
        }
    }
}

internal static class MoreGrassCompat
{
    private static readonly List<MoreGrassPackContext> packs = [];

    internal static void Register(IModHelper helper)
    {
        foreach (IContentPack contentPack in helper.ContentPacks.GetOwned())
        {
            if (MoreGrassPackContext.Make(contentPack) is MoreGrassPackContext mgpCtx)
            {
                ModEntry.Log(
                    $"More Grass -> Grass Variety shim applied for {contentPack.Manifest.Name} ({contentPack.Manifest.UniqueID})",
                    LogLevel.Debug
                );
                packs.Add(mgpCtx);
            }
        }
        if (packs.Any())
            helper.Events.Content.AssetRequested += OnAssetRequested;
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        foreach (MoreGrassPackContext mgPack in packs)
        {
            mgPack.AssetRequested(e);
        }
    }
}
