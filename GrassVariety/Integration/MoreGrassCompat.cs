using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
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

        List<List<Texture2D>> sourceTx =
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
                if (srcTx.Width > GrassComp.SPRITE_WIDTH || srcTx.Height > GrassComp.SPRITE_HEIGHT)
                {
                    ModEntry.Log(
                        $"More Grass Shim got texture '{file}' with size {srcTx.Width}x{srcTx.Height}. It is larger than the expected size {GrassComp.SPRITE_WIDTH}x{GrassComp.SPRITE_HEIGHT} and will be skipped.",
                        LogLevel.Warn
                    );
                    continue;
                }
                sourceTx[(int)season].Add(srcTx);
            }
        }
        int count = sourceTx.Max(lst => lst.Count);
        if (count == 0)
        {
            ModEntry.Log($"No grass sprites found in '{contentPack.Manifest.UniqueID}'");
            return null;
        }

        int height = GrassComp.SPRITE_HEIGHT * (isBlueGrass ? 12 : 5);
        int width = GrassComp.SPRITE_WIDTH * count;

        Texture2D spriteSheet = new(Game1.graphics.GraphicsDevice, width, height);

        Color[] srcData = new Color[GrassComp.SPRITE_WIDTH * GrassComp.SPRITE_HEIGHT];
        int xOffset = 0;

        List<string> includeSeason = [];

        for (int i = 0; i < sourceTx.Count; i++)
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

            List<Texture2D> srcList = sourceTx[i];
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
                Texture2D srcTx = srcList[j];
                srcTx.GetData(srcData, 0, srcData.Length);
                foreach (int yOffset in yOffsets)
                {
                    spriteSheet.SetData(
                        0,
                        new Rectangle(
                            xOffset,
                            yOffset
                                + (srcTx.Height < GrassComp.SPRITE_HEIGHT ? GrassComp.SPRITE_HEIGHT - srcTx.Height : 0),
                            GrassComp.SPRITE_WIDTH,
                            GrassComp.SPRITE_HEIGHT
                        ),
                        srcData,
                        0,
                        srcData.Length
                    );
                }
                xOffset += GrassComp.SPRITE_WIDTH;
                j++;
                if (j >= srcList.Count)
                {
                    j = 0;
                }
            }
        }

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
        spriteSheet.Name = grassVarietyData.Texture;
        return new MoreGrassPackContext(contentPack, spriteSheet, grassVarietyData);
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

internal static class MoreGrassShim
{
    private static List<MoreGrassPackContext> packs = [];

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
