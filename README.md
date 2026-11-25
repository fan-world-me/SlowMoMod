# SlowMo

A Hollow Knight: Silksong mod that slows down the world while keeping the hero at normal speed.

## Controls

- **Left Shift** — slow motion: the world slows down, the hero stays at normal speed.
- **Right Shift** — full slow motion: everything including the hero slows down.
- Press the same key again to disable.

## Build

Requires .NET SDK and the Silksong game libraries (via `Silksong.GameLibs`).

```bash
dotnet build
```

The build also packages the mod for Thunderstore into `thunderstore/dist`.