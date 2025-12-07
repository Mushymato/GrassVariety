using System.Buffers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.TerrainFeatures;

namespace GrassVariety;

internal static class GrassComp
{
    private const int MAX_PER_ROW = 273;
    private const int MAX_PER_COL = 17;
    internal const int Y_HEIGHT = 240;
    internal const int SPRITE_WIDTH = 15;
    internal const int SPRITE_HEIGHT = 20;

    internal static bool IsTxValid { get; set; } = false;
    internal static readonly List<IAssetName> sourceAssets = [];
    private static readonly List<Texture2D> spriteSheets = [];

    private static List<GrassVarietyData>? validVarieties = null;
    private static List<Point>? varietyMaxIndex = null;

    internal static HashSet<byte> GetOffsetY(HashSet<byte>? applyTo)
    {
        if (applyTo == null || applyTo.Count == 0)
        {
            return [0, 1, 2, 4];
        }

        HashSet<byte> offsetY = [];
        if (applyTo.Contains(Grass.springGrass))
        {
            offsetY.Add(0);
            offsetY.Add(1);
            offsetY.Add(2);
            offsetY.Add(4);
        }
        if (applyTo.Contains(Grass.caveGrass))
        {
            offsetY.Add(3);
        }
        if (applyTo.Contains(Grass.frostGrass))
        {
            offsetY.Add(4);
        }
        if (applyTo.Contains(Grass.lavaGrass))
        {
            offsetY.Add(5);
        }
        if (applyTo.Contains(Grass.caveGrass2))
        {
            offsetY.Add(6);
        }
        if (applyTo.Contains(Grass.cobweb))
        {
            offsetY.Add(7);
        }
        if (applyTo.Contains(Grass.blueGrass))
        {
            offsetY.Add(8);
            offsetY.Add(9);
            offsetY.Add(10);
            offsetY.Add(11);
        }
        return offsetY;
    }

    internal static void RecalculateSpriteSheet()
    {
        sourceAssets.Clear();
        validVarieties = AssetManager
            .RawGrassVarieties.Values.Where(variety =>
                variety.EnableAtlasOptimization
                && variety.Weight > 0
                && !(string.IsNullOrEmpty(variety.Texture) || !Game1.content.DoesAssetExist<Texture2D>(variety.Texture))
            )
            .ToList();
        varietyMaxIndex = [new(0, 0)];

        int mergedSheetIndex = 0;
        foreach (GrassVarietyData variety in validVarieties)
        {
            int subVariantCount =
                variety.SubVariants == null ? 3 : variety.SubVariants.Max() - variety.SubVariants.Min() + 1;
            Point currCompSheetCoord = varietyMaxIndex[^1];
            if (currCompSheetCoord.X + subVariantCount > MAX_PER_ROW)
            {
                currCompSheetCoord.X = 0;
                currCompSheetCoord.Y++;
                if (currCompSheetCoord.Y >= MAX_PER_COL)
                {
                    mergedSheetIndex++;
                    currCompSheetCoord = new(0, 0);
                    varietyMaxIndex.Add(currCompSheetCoord);
                }
            }
            variety.MergedSheetNum = mergedSheetIndex;
            variety.CompSheetCoord = new(currCompSheetCoord.X, currCompSheetCoord.Y);

            currCompSheetCoord.X += subVariantCount;

            varietyMaxIndex[^1] = currCompSheetCoord;
        }
    }

    private static void RecombineSpriteSheet()
    {
        if (varietyMaxIndex == null || validVarieties == null)
        {
            RecalculateSpriteSheet();
        }
        for (int i = 0; i < varietyMaxIndex!.Count; i++)
        {
            Point currCompSheetCoord = varietyMaxIndex[i];
            int width = currCompSheetCoord.Y >= 1 ? MAX_PER_ROW * SPRITE_WIDTH : currCompSheetCoord.X * SPRITE_WIDTH;
            int height = (currCompSheetCoord.Y + 1) * Y_HEIGHT;

            Texture2D compTx;
            if (spriteSheets.Count > i)
            {
                compTx = spriteSheets[i];
                if (compTx.Width < width)
                {
                    using Texture2D tempTx = new(Game1.graphics.GraphicsDevice, width, height);
                    compTx.CopyFromTexture(tempTx);
                }
            }
            else
            {
                compTx = new(Game1.graphics.GraphicsDevice, width, height);
                spriteSheets.Add(compTx);
            }
            Color[] targetData = new Color[compTx.GetElementCount()];
            Array.Fill(targetData, Color.Transparent);

            foreach (GrassVarietyData variety in validVarieties!)
            {
                if (variety.MergedSheetNum != i)
                    continue;

                sourceAssets.Add(ModEntry.ParseAssetName(variety.Texture));

                Texture2D sourceTexture = Game1.content.Load<Texture2D>(variety.Texture);
                Color[] sourceData = ArrayPool<Color>.Shared.Rent(sourceTexture.GetElementCount());
                sourceTexture.GetData(sourceData, 0, sourceTexture.GetElementCount());

                foreach (byte applyTo in GetOffsetY(variety.ApplyTo))
                {
                    if (variety.SubVariants == null)
                    {
                        CopySourceSpriteToTarget(
                            ref sourceData,
                            sourceTexture.Width,
                            new(SPRITE_WIDTH, SPRITE_HEIGHT * applyTo, SPRITE_WIDTH * 3, SPRITE_HEIGHT),
                            ref targetData,
                            compTx.Width,
                            new(
                                SPRITE_WIDTH * variety.CompSheetCoord.X,
                                variety.CompSheetCoord.Y * Y_HEIGHT + SPRITE_HEIGHT * applyTo,
                                SPRITE_WIDTH * 3,
                                SPRITE_HEIGHT
                            )
                        );
                    }
                    else
                    {
                        foreach (int j in variety.SubVariants.ToHashSet())
                        {
                            CopySourceSpriteToTarget(
                                ref sourceData,
                                sourceTexture.Width,
                                new(SPRITE_WIDTH * j, SPRITE_HEIGHT * applyTo, SPRITE_WIDTH, SPRITE_HEIGHT),
                                ref targetData,
                                compTx.Width,
                                new(
                                    SPRITE_WIDTH * (variety.CompSheetCoord.X + j),
                                    variety.CompSheetCoord.Y * Y_HEIGHT + SPRITE_HEIGHT * applyTo,
                                    SPRITE_WIDTH,
                                    SPRITE_HEIGHT
                                )
                            );
                        }
                    }
                }

                ArrayPool<Color>.Shared.Return(sourceData);
            }

            compTx.SetData(targetData);
            compTx.Name = $"{ModEntry.ModId}/Comp/{i}";
        }
        IsTxValid = true;
    }

    private static void CopySourceSpriteToTarget(
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
        varietyMaxIndex = null;
        validVarieties = null;
        IsTxValid = false;
    }

    internal static void CheckInvalidate(IReadOnlySet<IAssetName> names)
    {
        if (names.Any(sourceAssets.Contains))
        {
            IsTxValid = false;
            return;
        }
    }
}
