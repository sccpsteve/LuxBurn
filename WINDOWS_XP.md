# LuxBurn compatibility build

LuxBurn includes a separate compatibility build in `OpenBurningSuite.Xp`.

The main Avalonia application targets .NET 8 and cannot run on Windows XP. The XP build targets .NET Framework 4.0 and uses WinForms so it can run on Windows XP SP3 systems with the .NET Framework 4.0 runtime installed.

## Requirements

- Windows XP SP3
- .NET Framework 4.0
- Microsoft IMAPI2 update for image building and burning

The application still starts when IMAPI2 is not installed. In that state, checksum verification remains available, while drive discovery, ISO building, and burning report that the Windows imaging component is missing.

## Build

From the repository root, use the plug-and-play build script:

```cmd
build-xp.cmd
```

This script uses the .NET 4 compiler installed with the framework and does not require the modern .NET SDK to locate a .NET Framework 4.0 targeting pack.

If you have a full .NET Framework 4.0 Developer Pack installed, this MSBuild command also works:

```powershell
dotnet msbuild OpenBurningSuite.Xp\OpenBurningSuite.Xp.csproj /p:Configuration=Release /p:Platform=x86
```

The output is written to:

```text
OpenBurningSuite.Xp\bin\Release\LuxBurn.exe
```

## Launch

From the repository root:

```cmd
run-xp.cmd
```

The script builds the XP executable if needed and starts it.

You can also start the built program directly:

```cmd
OpenBurningSuite.Xp\bin\Release\LuxBurn.exe
```

Do not use `dotnet run` for the XP build. `dotnet run` is for the modern Avalonia/.NET app:

```cmd
dotnet run --project OpenBurningSuite
```

## Design notes

The XP build intentionally uses a restrained utility layout: classic controls, Tahoma, compact grouped settings, plain labels, and a persistent log. It avoids the modern app's dark dashboard presentation so the legacy build feels like a practical Windows tool rather than a generated demo surface.
