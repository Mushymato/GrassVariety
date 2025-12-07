# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2025-12-07

### Added

- More Grass packs are now supported via compat shim option.

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
