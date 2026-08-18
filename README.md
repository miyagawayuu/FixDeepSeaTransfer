# FixDeepSeaTransfer

FixDeepSeaTransfer is a standalone plugin for Rust servers running
[Carbon](https://github.com/CarbonCommunity/Carbon) or
[Oxide](https://github.com/OxideMod/Oxide.Rust). It prevents passengers of a `PlayerBoat` from being
stranded or killed while the boat transfers into or out of the Deep Sea.

Current version: `0.3.0`

## Fixes

- Uses `PlayerBoat.Teleport` when transferring a `PlayerBoat`.
- Levels the boat while preserving its heading so an already-leaning boat does not arrive capsized.
- Captures passengers and their boat-relative positions before entering the Deep Sea.
- Synchronizes every passenger to the boat's destination before Rust dismounts and remounts them.
- Uses a scoped lightweight dismount during Deep Sea transfers to prevent Rust's
  invalid-dismount-position suicide damage.
- Recovers passengers left behind when only their boat enters the Deep Sea.
- Recovers passengers left behind when only their boat exits to the mainland.
- Rechecks exit transfers once after 0.1 seconds to handle synchronization races.

The lightweight dismount override applies only to passengers captured in an active
Deep Sea transfer. Normal dismounts and transfers for other vehicles are unchanged.

## Installation

Download `FixDeepSeaTransfer.cs` from the latest GitHub Release and copy it into
the plugin directory for your mod loader:

- Carbon: `carbon/plugins`
- Oxide: `oxide/plugins`

Do not normally load this plugin together with `SkillDeepSea`, because both
plugins contain the same Harmony patches and would run duplicate corrections.

Restart the server or reload the plugin (`c.reload FixDeepSeaTransfer` on Carbon
or `oxide.reload FixDeepSeaTransfer` on Oxide). Back up your server
configuration and test updates in a non-production environment first.

## Compatibility

- Rust Dedicated Server, public/release branch
- Carbon production build
- Oxide production build

Rust, Carbon, and Oxide updates can change internal methods used by this plugin. The
GitHub Actions build verifies the plugin against the current public Rust server
and current production assemblies for both mod loaders, but it does not replace
in-game testing.

## Local build

```powershell
dotnet build .\FixDeepSeaTransfer.csproj -c Release -p:RustDir=C:\path\to\rustserver -p:ModLoader=Carbon
dotnet build .\FixDeepSeaTransfer.csproj -c Release -p:RustDir=C:\path\to\rustserver -p:ModLoader=Oxide
```

`RustDir` must contain a Rust Dedicated Server installation and the selected mod
loader. The compiled DLL is only a build-time compatibility check; Carbon and
Oxide install this project as the single source file in `src/FixDeepSeaTransfer`.

## Releases

Every pull request and push to `main` runs Release configuration builds against
both Carbon and Oxide. After both builds succeed on `main`, GitHub Actions creates
the version tag from the plugin's `Info` attribute if that tag does not exist,
then creates or updates a GitHub Release containing:

- `FixDeepSeaTransfer.cs`
- `SHA256SUMS.txt`

Existing tags are never moved. Increment the version in the plugin's `Info`
attribute before publishing a new release.

## Privacy

The plugin does not make outbound network requests or collect telemetry. It logs
the Rust player and vehicle IDs only when it recovers a stranded passenger.

## License

Licensed under the [MIT License](LICENSE).
