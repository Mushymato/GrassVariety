# Grass Variety

New grass framework.

## For Authors

See [author-guilde.md](docs/author-guide.md).

## For Players

### Installation

1. Install SMAPI and Content Patcher
2. Install this mod by extracting it and placing it in your `Mods` folder.

### Config

The `DefaultGrassWeight` config changes how frequently the vanilla grass texture (`TerrainFeatures/grass`) appear. The default is 0, but you may use this to allow some compatibility between content packs using this mod and content packs that directly retextures the grass.

At the moment it can be set via console command `gv-default_grass_weight [weight]` or by editing `config.json`, no GMCM support for now.

## Compatiblity

This mod is not compatible with [More Grass](https://www.nexusmods.com/stardewvalley/mods/5398) which patches draw directly and takes effect over this mod completely. There are no plans to introduce direct compatibility due to feature overlap and conflicting design choices.

However, you can have Grass Variety load content packs made for More Grass by changing some manifest fields, [see here for how to](./docs/more-grass-shim.md).
