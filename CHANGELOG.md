# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.5]

### Fixed

- Exception when there's no grass packs (lol).
- More null checking (for mysterious split screen problem)
- Texture sheet getting disposed when returning to title screen while not in english

## [1.2.4] - 2026-01-01

### Fixed

- Allow some limited MP grass mismatch to occur, though it is still not recommended.

## [1.2.3] - 2025-12-08

### Fixed

- Reduce composite texture memory usage.

## [1.2.2] - 2025-12-08

### Fixed

- Fix debris using non-existent texture and crashing draw loop.

## [1.2.1] - 2025-12-08

### Fixed

- Check for texture size 15x20 in more grass shim.
- Reset textures when unsetting grass.

## [1.2.0] - 2025-12-07

### Added

- More Grass packs are now supported via compat shim option.

### Changed

- Performance improvements by reducing texture swapping, can be disabled if needed via `EnableAtlasOptimization`.

## [1.1.2] - 2025-10-14

### Fixed

- Occasional crash in split-screen on warp location.

## [1.1.1] - 2025-09-25

### Added

- New field InheritOnCutFrom, define another grass to inherit OnCutItemSpawns and OnCutTileActions from (DEFAULT if unset)

### Changed

- Move grass watching logic to a watcher class

## [1.1.0] - 2025-09-23

### Added

- New field on grass variety `"PersistDays"` for defining how long a non-forced grass variety will persist
- New field on grass item query `"RequiresScythe"`, requires the scythe if true

### Fixed

- DEFAULT variant not saving the chosen variety properly
- Bomb related NRE

## [1.0.0] - 2025-09-21

### Added

- Initial Release
