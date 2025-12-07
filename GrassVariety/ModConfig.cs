using MachineControlPanel.Integration;
using StardewModdingAPI;

namespace GrassVariety;

public sealed class ModConfig
{
    public int DefaultGrassWeight { get; set; } = 0;
    public int MoreGrassShimWeight { get; set; } = 3;

    public void Reset()
    {
        DefaultGrassWeight = 0;
        MoreGrassShimWeight = 3;
    }

    internal void Register(IModHelper helper, IManifest mod, IGenericModConfigMenuApi gmcm)
    {
        gmcm.Register(
            mod,
            () =>
            {
                Reset();
                helper.WriteConfig(this);
            },
            () =>
            {
                helper.WriteConfig(this);
            }
        );
        gmcm.AddNumberOption(
            mod,
            () => DefaultGrassWeight,
            (value) =>
            {
                DefaultGrassWeight = Math.Max(0, value);
                helper.GameContent.InvalidateCache(AssetManager.Asset_GrassVariety);
            },
            name: () => helper.Translation.Get("config.DefaultGrassWeight.name"),
            tooltip: () => helper.Translation.Get("config.DefaultGrassWeight.desc"),
            min: 0
        );
        gmcm.AddNumberOption(
            mod,
            () => MoreGrassShimWeight,
            (value) =>
            {
                MoreGrassShimWeight = Math.Max(0, value);
                helper.GameContent.InvalidateCache(AssetManager.Asset_GrassVariety);
            },
            name: () => helper.Translation.Get("config.MoreGrassShimWeight.name"),
            tooltip: () => helper.Translation.Get("config.MoreGrassShimWeight.desc"),
            min: 0
        );
    }
}
