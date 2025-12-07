# More Grass Shim

> [!WARNING]  
> If you are **creating a new mod**, please follow instructions on [author guide](./author-guide.md) instead!

Grass Variety has the ability to utilize content packs created for More Grass, with these limitations:
1. Nothing specific to the `content.json` of More Grass content pack is supported, that means no location blacklist/whitelist settings.
2. The player facing config options of More Grass don't exist in Grass Variety, because Grass Variety gives this power to content packs instead.
3. There's no way to dynamically change settings for More Grass packs loaded this way.

## manifest.json

To use an existing More Grass pack with Grass Variety, you should:
1. Edit the manifest such that the content pack is for `mushymato.GrassVariety` instead of `EpicBellyFlop45.MoreGrass`.
2. DO NOT use More Grass and Grass Variety together as they are incompatible, but DO [endorse More Grass on nexus](https://www.nexusmods.com/stardewvalley/mods/5398)!

Example manifest.json from [Wildflower Grass Field](https://www.nexusmods.com/stardewvalley/mods/5407)

```json
{
  // No change needed:
  "Name": "Wildflower Grass Field",
  "Author": "DustBeauty",
  "Version": "1.1",
  "Description": "Adds 13 grass variations to each season, a new asset handler for winter grass to work with reset terrian features.",
  "UniqueID": "DustBeauty.WildflowerGrassField",
  "MinimumApiVersion": "3.0.0",
  "UpdateKeys": [
    "Nexus:5407"
  ],

  // Parts to change:
  "ContentPackFor": {
    // Change from "UniqueID": "EpicBellyFlop45.MoreGrass"
    "UniqueID": "mushymato.GrassVariety"
  },

  // Optional fields:
  // By default, More Grass packs are converted only for regular grass (springGrass) and does not apply to blue grass (blueGrass)
  // This can be changed via these extra manifest fields:
  "mushymato.GrassVariety/IsGreenGrass": true, // true will make this pack valid for green grass (default true)
  "mushymato.GrassVariety/IsBlueGrass": false, // true will make this pack valid for blue grass (default false)
  "mushymato.GrassVariety/Weight": 1, // changes how often this grass appears relative to all other grass
}
```

## Converting Packs

If you wish to tweak more settings in the grass pack, you should convert it to Content Patcher/Grass Variety format first.

After loading into the game, you can do console command `patch export mushymato.GrassVariety/Data` to get the combined grass variety data.

The entries that are created by the compat shim have key format of `mushymato.GrassVariety@<more grass pack mod ID>@MoreGrassShim`, they can generally be copied directly into a content patcher content.json, after which you may modify Grass Variety specific fields.

The combined texture is also listed on the entry with format `mushymato.GrassVariety/<more grass pack mod ID>/MoreGrassSheet`, this can also be saved as a PNG via `patch export mushymato.GrassVariety/<more grass pack mod ID>/MoreGrassSheet` to be included with your converted Content Patcher/Grass Variety pack.
