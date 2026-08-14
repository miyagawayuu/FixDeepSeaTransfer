# FixDeepSeaTransfer

FixDeepSeaTransfer is a standalone [Carbon](https://github.com/CarbonCommunity/Carbon)
plugin for Rust servers. It prevents passengers of a `PlayerBoat` from being
stranded or killed while the boat transfers into or out of the Deep Sea.

Current version: `0.1.1`

## Fixes

- Uses `PlayerBoat.Teleport` when transferring a `PlayerBoat`.
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
the server's `carbon/plugins` directory.

Do not normally load this plugin together with `SkillDeepSea`, because both
plugins contain the same Harmony patches and would run duplicate corrections.

Restart the server or reload the plugin after installation. Back up your server
configuration and test updates in a non-production environment first.

## Compatibility

- Rust Dedicated Server, public/release branch
- Carbon production build

Rust and Carbon updates can change internal methods used by this plugin. The
GitHub Actions build verifies the plugin against the current public Rust server
and current Carbon production assemblies, but it does not replace in-game testing.

## Local build

```powershell
dotnet build .\FixDeepSeaTransfer.csproj -c Release -p:RustDir=C:\path\to\rustserver
```

`RustDir` must contain both a Rust Dedicated Server installation and Carbon.
The compiled DLL is only a build-time compatibility check; Carbon installs this
project as the single source file in `src/FixDeepSeaTransfer`.

## Releases

Every pull request and push to `main` runs a Release configuration build. After a
successful `main` build, GitHub Actions creates the version tag from the plugin's
`Info` attribute if that tag does not exist, then creates or updates a GitHub
Release containing:

- `FixDeepSeaTransfer.cs`
- `SHA256SUMS.txt`

Existing tags are never moved. Increment the version in the plugin's `Info`
attribute before publishing a new release.

## Privacy

The plugin does not make outbound network requests or collect telemetry. It logs
the Rust player and vehicle IDs only when it recovers a stranded passenger.

## License

Licensed under the [MIT License](LICENSE).

