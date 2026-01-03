using System.Buffers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.TerrainFeatures;

namespace GrassVariety;

internal sealed class PostionOnComp()
{
    public int Sheet = 0;
    public int X = 0;
    public HashSet<int> XIndexes = [];
    public int Y = 0;
    public int XBase = 0;

    public void PopulateXIndexes(List<int>? subVariants)
    {
        if (subVariants == null)
        {
            XIndexes.Add(0);
            XIndexes.Add(1);
            XIndexes.Add(2);
        }
        else if (subVariants.Count > 0)
        {
            XIndexes.AddRange(subVariants);
        }
    }
}

internal sealed class PositionOnCompGroup()
{
    public bool IsTxValid = false;
    public PostionOnComp[] Arr = [new(), new(), new(), new(), new(), new(), new()];
}

internal static class GrassComp
{
    private const int MAX_PER_ROW = 273;
    private const int MAX_PER_COL = 17;
    internal const int Y_HEIGHT = 240;
    internal const int SPRITE_WIDTH = 15;
    internal const int SPRITE_HEIGHT = 20;

    private static bool IsTxValid = false;
    private static readonly List<Texture2D> spriteSheets = [];
    private static Dictionary<IAssetName, PositionOnCompGroup>? assetToPosOnComp = null;

    private static Dictionary<IAssetName, PositionOnCompGroup> GetAssetToPosOnComp()
    {
        if (assetToPosOnComp != null)
            return assetToPosOnComp;

        assetToPosOnComp = [];
        List<GrassVarietyData> validVarieties = AssetManager
            .RawGrassVarieties.Values.Where(variety =>
                variety.EnableAtlasOptimization
                && variety.Weight > 0
                && !(string.IsNullOrEmpty(variety.Texture) || !Game1.content.DoesAssetExist<Texture2D>(variety.Texture))
            )
            .ToList();

        // first pass gather requirements
        foreach (GrassVarietyData data in validVarieties)
        {
            IAssetName assetName = ModEntry.content.ParseAssetName(data.Texture);
            if (!assetToPosOnComp.TryGetValue(assetName, out PositionOnCompGroup? posOnCompGroup))
            {
                posOnCompGroup = new();
                assetToPosOnComp[assetName] = posOnCompGroup;
            }
            if (data.ApplyTo == null || data.ApplyTo.Count == 0)
            {
                posOnCompGroup.Arr[Grass.springGrass - 1].PopulateXIndexes(data.SubVariants);
            }
            else
            {
                foreach (byte applyTo in data.ApplyTo)
                {
                    posOnCompGroup.Arr[applyTo - 1].PopulateXIndexes(data.SubVariants);
                }
            }
            data.PosOnCompArray = posOnCompGroup.Arr;
        }

        // second pass assign sheet/X/Y
        int maxSheet = 0;
        int maxSheetX = 0;
        int maxSheetY = 0;
        for (byte applyTo = Grass.springGrass; applyTo <= Grass.blueGrass; applyTo++)
        {
            int sheet = 0;
            int x = 0;
            int y = 0;
            foreach (PositionOnCompGroup posOnCompGroup in assetToPosOnComp.Values)
            {
                PostionOnComp posOnComp = posOnCompGroup.Arr[applyTo - 1];
                if (posOnComp.XIndexes.Count == 0)
                    continue;
                int xIncrease = posOnComp.XIndexes.Max() - posOnComp.XIndexes.Min() + 1;
                if (x + xIncrease >= MAX_PER_ROW)
                {
                    maxSheetX = Math.Max(x, maxSheetX);
                    x = 0;
                    y++;
                    if (y >= MAX_PER_COL)
                    {
                        y = 0;
                        sheet++;
                        maxSheetX = 0;
                        maxSheetY = 0;
                    }
                }
                posOnComp.Sheet = sheet;
                posOnComp.X = x;
                posOnComp.Y = y;

                x += xIncrease;
            }
            if (sheet >= maxSheet)
            {
                maxSheet = sheet;
                maxSheetX = Math.Max(x, maxSheetX);
                maxSheetY = Math.Max(y, maxSheetY);
            }
        }

        if (maxSheetX == 0 && maxSheetY == 0)
            return assetToPosOnComp;

        // create the sheets
        for (int i = 0; i <= maxSheet; i++)
        {
            int width = (i == maxSheet) ? (maxSheetX * SPRITE_WIDTH) : 4096;
            int height = i == maxSheet ? ((maxSheetY + 1) * Y_HEIGHT) : 4096;
            Texture2D targetTexture;
            if (spriteSheets.Count > i)
            {
                targetTexture = spriteSheets[i];
                if (targetTexture.Width < width || targetTexture.Height < height)
                {
                    using Texture2D tempTx = new(Game1.graphics.GraphicsDevice, width, height);
                    targetTexture.CopyFromTexture(tempTx);
                }
            }
            else
            {
                targetTexture = new Texture2D(Game1.game1.GraphicsDevice, width, height)
                {
                    Name = $"{ModEntry.ModId}/comp_{i}",
                };
                spriteSheets.Add(targetTexture);
            }
            Color[] targetData = ArrayPool<Color>.Shared.Rent(targetTexture.GetElementCount());
            Array.Fill(targetData, Color.ForestGreen);
            targetTexture.SetData(targetData, 0, targetTexture.GetElementCount());
        }

        return assetToPosOnComp;
    }

    internal static IEnumerable<int> ApplyToYIndex(byte applyTo)
    {
        switch (applyTo)
        {
            case Grass.springGrass:
                yield return 0;
                yield return 1;
                yield return 2;
                yield return 4;
                break;
            case Grass.caveGrass:
                yield return 3;
                break;
            case Grass.frostGrass:
                yield return 4;
                break;
            case Grass.lavaGrass:
                yield return 5;
                break;
            case Grass.caveGrass2:
                yield return 6;
                break;
            case Grass.cobweb:
                yield return 7;
                break;
            case Grass.blueGrass:
                yield return 8;
                yield return 9;
                yield return 10;
                yield return 11;
                break;
        }
    }

    internal static void RecombineSpriteSheet()
    {
        Dictionary<IAssetName, PositionOnCompGroup> AssetToPosOnComp = GetAssetToPosOnComp();
        for (int i = 0; i < spriteSheets.Count; i++)
        {
            Texture2D targetTexture = spriteSheets[i];
            Color[] targetData = ArrayPool<Color>.Shared.Rent(targetTexture.GetElementCount());
            targetTexture.GetData(targetData, 0, targetTexture.GetElementCount());

            foreach ((IAssetName asset, PositionOnCompGroup posOnCompGroup) in AssetToPosOnComp)
            {
                if (posOnCompGroup.IsTxValid)
                    continue;

                Texture2D sourceTexture = Game1.content.Load<Texture2D>(asset.BaseName);
                Color[] sourceData = ArrayPool<Color>.Shared.Rent(sourceTexture.GetElementCount());
                sourceTexture.GetData(sourceData, 0, sourceTexture.GetElementCount());
                for (byte applyTo = Grass.springGrass; applyTo <= Grass.blueGrass; applyTo++)
                {
                    PostionOnComp posOnComp = posOnCompGroup.Arr[applyTo - 1];
                    if (posOnComp.XIndexes.Count == 0)
                        continue;
                    if (posOnComp.Sheet != i)
                        continue;

                    posOnComp.XBase = posOnComp.X - posOnComp.XIndexes.Min();
                    foreach (int yindex in ApplyToYIndex(applyTo))
                    {
                        foreach (int xindex in posOnComp.XIndexes)
                        {
                            CopySourceSpriteToTarget(
                                ref sourceData,
                                sourceTexture.Width,
                                new Rectangle(
                                    xindex * SPRITE_WIDTH,
                                    yindex * SPRITE_HEIGHT,
                                    SPRITE_WIDTH,
                                    SPRITE_HEIGHT
                                ),
                                ref targetData,
                                targetTexture.Width,
                                new Rectangle(
                                    (posOnComp.XBase + xindex) * SPRITE_WIDTH,
                                    posOnComp.Y * Y_HEIGHT + yindex * SPRITE_HEIGHT,
                                    SPRITE_WIDTH,
                                    SPRITE_HEIGHT
                                )
                            );
                        }
                    }
                }
                posOnCompGroup.IsTxValid = true;
                ArrayPool<Color>.Shared.Return(sourceData);
            }
            targetTexture.SetData(targetData, 0, targetTexture.GetElementCount());
            ArrayPool<Color>.Shared.Return(targetData);
        }

        IsTxValid = true;
    }

    internal static void CopySourceSpriteToTarget(
        ref Color[] sourceData,
        int sourceTxWidth,
        Rectangle sourceRect,
        ref Color[] targetData,
        int targetTxWidth,
        Rectangle targetRect
    )
    {
        // ModEntry.Log($"Copy src{sourceRect} to dst{targetRect}");
        // r is row, aka y
        // copy the array row by row from source to target
        for (int r = 0; r < SPRITE_HEIGHT; r++)
        {
            int sourceArrayStart = sourceRect.X + (sourceRect.Y + r) * sourceTxWidth;
            int targetArrayStart = targetRect.X + (targetRect.Y + r) * targetTxWidth;
            if (sourceArrayStart + SPRITE_WIDTH > sourceData.Length)
            {
                Array.Fill(targetData, Color.Transparent, targetArrayStart, SPRITE_WIDTH);
            }
            else
            {
                Array.Copy(sourceData, sourceArrayStart, targetData, targetArrayStart, SPRITE_WIDTH);
            }
        }
    }

    internal static Texture2D LoadTexture(int mergedSheetIndex)
    {
        if (!IsTxValid)
        {
            RecombineSpriteSheet();
        }
        return spriteSheets[mergedSheetIndex];
    }

    internal static void InvalidateData()
    {
        assetToPosOnComp = null;
        IsTxValid = false;
    }

    internal static void CheckInvalidate(IReadOnlySet<IAssetName> names)
    {
        if (assetToPosOnComp == null)
            return;
        foreach (IAssetName name in names)
        {
            if (assetToPosOnComp.TryGetValue(name, out PositionOnCompGroup? posOnCompGroup))
            {
                posOnCompGroup.IsTxValid = false;
                IsTxValid = false;
            }
        }
    }

    /// Based on https://github.com/Pathoschild/StardewMods/blob/95d695b205199de4bad86770d69a30806d1721a2/ContentPatcher/Framework/Commands/Commands/ExportCommand.cs
    /// MIT License
    #region PATCH_EXPORT
    internal static void Export(IModHelper helper, string exportDir)
    {
        if (!IsTxValid)
        {
            RecombineSpriteSheet();
        }
        Dictionary<IAssetName, PositionOnCompGroup> assetToPosOnComp = GetAssetToPosOnComp();
        helper.Data.WriteJsonFile("export/comp.json", assetToPosOnComp);
        ModEntry.Log($"Export to {exportDir}", LogLevel.Info);
        ModEntry.Log($"- comp.json", LogLevel.Info);
        foreach (Texture2D targetTexture in spriteSheets)
        {
            string pngName = SanitizePath(string.Concat(Path.GetFileName(targetTexture.Name)) + ".png");
            using Texture2D exported = UnPremultiplyTransparency(targetTexture);
            using Stream stream = File.Create(Path.Combine(exportDir, pngName));
            exported.SaveAsPng(stream, exported.Width, exported.Height);
            ModEntry.Log($"- {pngName}", LogLevel.Info);
        }
    }

    private static string SanitizePath(string path)
    {
        return string.Join('_', path.Split(Path.GetInvalidFileNameChars()));
    }

    /// <summary>Reverse premultiplication applied to an image asset by the XNA content pipeline.</summary>
    /// <param name="texture">The texture to adjust.</param>
    private static Texture2D UnPremultiplyTransparency(Texture2D texture)
    {
        Color[] data = new Color[texture.Width * texture.Height];
        texture.GetData(data);

        for (int i = 0; i < data.Length; i++)
        {
            Color pixel = data[i];
            if (pixel.A == 0)
                continue;

            data[i] = new Color(
                (byte)(pixel.R * 255 / pixel.A),
                (byte)(pixel.G * 255 / pixel.A),
                (byte)(pixel.B * 255 / pixel.A),
                pixel.A
            ); // don't use named parameters, which are inconsistent between MonoGame (e.g. 'alpha') and XNA (e.g. 'a')
        }

        Texture2D result = new(texture.GraphicsDevice ?? Game1.graphics.GraphicsDevice, texture.Width, texture.Height);
        result.SetData(data);
        return result;
    }
    #endregion
}
