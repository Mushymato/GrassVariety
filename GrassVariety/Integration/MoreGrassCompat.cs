using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace GrassVariety.Integration;

#region MoreGrass.Config.ContentPackConfig
/*
MIT License

Copyright (c) 2019 EpicBellyFlop45

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

/// <summary>More Grass content pack configuration.</summary>
public sealed class ContentPackConfig
{
    /*********
    ** Properties
    *********/
    /// <summary>Whether default grass sprites should be drawn too.</summary>
    /// <remarks>GrassVariety: Ignored</remarks>
    public bool EnableDefaultGrass { get; set; } = true;

    /// <summary>The locations that each specified grass is allowed to be in.</summary>
    /// <remarks>GrassVariety: Transform to condition</remarks>
    public Dictionary<string, List<string>> WhiteListedGrass { get; set; } = [];

    /// <summary>The locations that each specified grass isn't allowed to be in.</summary>
    /// <remarks>GrassVariety: Transform to condition</remarks>
    public Dictionary<string, List<string>> BlackListedGrass { get; set; } = [];

    /// <summary>The locations that this pack is allowed to retexture grass in.</summary>
    /// <remarks>GrassVariety: Transform to condition</remarks>
    public List<string> WhiteListedLocations { get; set; } = [];

    /// <summary>The locations that this pack isn't allowed to retexture grass is.</summary>
    /// <remarks>GrassVariety: Transform to condition</remarks>
    public List<string> BlackListedLocations { get; set; } = [];
}

#endregion

internal sealed class MoreGrassPackContext(
    IContentPack contentPack,
    Texture2D spriteSheet,
    GrassVarietyData grassVarietyData
)
{
    internal static MoreGrassPackContext? Make(IContentPack contentPack)
    {
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
                sourceTx[(int)season].Add(contentPack.ModContent.Load<Texture2D>(relFile));
            }
        }
        int count = sourceTx.Max(lst => lst.Count);
        if (count == 0)
        {
            ModEntry.Log($"No grass sprites found in '{contentPack.Manifest.UniqueID}'");
            return null;
        }

        int height = 100;
        int width = GrassComp.SPRITE_WIDTH * count;

        Texture2D spriteSheet = new(Game1.graphics.GraphicsDevice, width, height);

        Color[] srcData = new Color[GrassComp.SPRITE_WIDTH * GrassComp.SPRITE_HEIGHT];
        int xOffset = 0;
        int yOffset = 0;

        List<string> includeSeason = [];

        for (int i = 0; i < sourceTx.Count; i++)
        {
            yOffset = 20 * i;
            if (i == 3)
                yOffset += 20;
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
                spriteSheet.SetData(
                    0,
                    new Rectangle(
                        xOffset,
                        yOffset + (srcTx.Height < GrassComp.SPRITE_HEIGHT ? GrassComp.SPRITE_HEIGHT - srcTx.Height : 0),
                        GrassComp.SPRITE_WIDTH,
                        GrassComp.SPRITE_HEIGHT
                    ),
                    srcData,
                    0,
                    srcData.Length
                );
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
            Id = $"{contentPack.Manifest.UniqueID}_MoreGrassShim",
            Texture = $"{ModEntry.ModId}/{contentPack.Manifest.UniqueID}/MoreGrassSheet",
            Weight = ModEntry.Config.MoreGrassShimWeight,
            SubVariants = Enumerable.Range(0, count).ToList(),
            ApplyTo = [Grass.springGrass],
        };
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
