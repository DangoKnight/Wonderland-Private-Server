# Native Ground.MMG backdrops and running this fork

This fork is based on upstream `Eminbalci/Wonderland-Private-Server` Develop
commit `0d16ebb` (2026-11-05 review), synced from local `0cb6915`.

## What changed

Private Server previously mentioned `Ground.MMG` in its setup instructions and
used its existence as one signal when finding the data directory. No loader
consumed its backdrop metadata. PvE/team-PvP and the older duel entry point
selected scenery with a map-ID fallback instead.

`GroundBackdropCatalog` now reads the native named archive footer and decodes
each map's backdrop tail after terrain, sound zones and map objects. The tail
contains an unresolved byte, a default selector, a region count and nine-byte
records: selector byte plus left/top/right/bottom UInt16 coordinates. Alogin
`FUN_003f830c` loads these fields; `FUN_003fa0b8` displays their rectangles and
labels from scene `59000 + selector`.

Startup loads a complete immutable catalog once before accepting clients.
Combat chooses the initiating leader's map and position once for all recipients.
Ordered first-match regions have inclusive boundaries, matching Gonline;
closed Legacy server overlap tie-breaking remains unverified. No event, monster
species or manually assigned biome exceptions are required. AC11:250 writes
one selector byte followed by a separate zero compatibility metadata byte.
Both the main combat path and the older duel path use the same resolver.

Missing/corrupt archives log a warning and retain the previous complete catalog,
or the documented compatibility fallback on first load. Unknown scene selectors
are preserved for the client's existing fallback. Native map 58000 has selector
150, whose scene 59150 is absent from the supplied SceneData.

The supplied archive covers 1,147 terrain maps and 1,398 rectangles across
101 maps. All 1,119 EVE maps have matching terrain records. For example:

| Map | Position | Selector | Scene |
| --- | --- | --- | --- |
| 60001 | 2202,1395 | 91 | Lakeside Jungle |
| 60001 | 2000,400 | 90 | Jungle |
| 60001 | 5000,3000 | 50 | Beach |
| 60001 | 2622,755 | 81 | Desert Grassland |
| 60000 | 1694,963 | 50 | Beach |
| 12000 | default | 130 | Welling Street |
| 12002 | default | 140 | Room Scape |

## Windows build

Install Visual Studio 2022 or newer (or Build Tools) with **.NET desktop
development**, plus a .NET SDK for the SDK-style projects. This remains a
Windows Forms application targeting .NET Framework 4.6.2. Restore obtains the
reference-assemblies package; a .NET Framework 4.x runtime must be installed.
Use full Visual Studio MSBuild, as verified here, rather than assuming the
cross-platform `dotnet build` host supports every legacy project target.

From PowerShell:

```powershell
git clone https://github.com/DangoKnight/Wonderland-Private-Server.git
cd Wonderland-Private-Server
git switch Develop
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (!$msbuild) { throw 'Install Visual Studio MSBuild with .NET desktop development.' }
& $msbuild 'Wonderland Private Server.sln' /restore /p:RestorePackagesConfig=true /p:Configuration=Debug /m:1 /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
```

An existing checkout should fetch/pull the fork's `Develop` instead of cloning
again. Upstream remains available separately as
`https://github.com/Eminbalci/Wonderland-Private-Server.git`.

## Assets and database

Use the native client files in `Data`, including `eve.Emg`, `Ground.MMG`,
`SceneData.dat`, `Npc.dat`, `itemDat.wpdat` (the server's expected item export),
`Skill.dat`, `SkillData.MBTM`, `Talk.dat`, `Mark.dat`, `Formula.dat`,
`Compound.dat` and `Compound2.dat`. Retain the repository's supplementary tables.
No Gonline asset extraction or Gonline database migration command is involved.
The backdrop loader requires the original `Ground.MMG`, not `ground_data.json`.

Alternatively, put the absolute native-data directory in `data_path.txt` beside
the executable. The executable-directory override takes precedence over the
repository-root override. Existing overrides are preserved by this patch.

The supplied `database.override.txt` selects SQLite (`Type|1`) and
`File|ServerDataBase.db`. The normal data resolver finds `Data/ServerDataBase.db`.
**An executable-directory `database.override.txt` takes precedence**; inspect
`bin/Debug/database.override.txt` if the running server uses another file.
To select an unambiguous existing SQLite database, use its absolute path:

```text
Type|1
User|
Pass|
DB|
Port|
IP|
File|C:\WLO\Data\ServerDataBase.db
```

Place this beside the executable, adjusting the path. Keep a backup before
starting with an existing gameplay database: startup verifies/creates tables
and performs the upstream initialization behavior. This fork does not migrate
or share Gonline's assets/gameplay databases.

## Start and connect

Stop Gonline and any other local server first if they use the same ports.
The active login/gameplay socket is TCP 6414, status uses TCP 6416 and
registration uses HTTP 8080. Although the GUI also displays 6415, this checkout
has no active world listener on that port. Run:

```powershell
& '.\bin\Debug\Wonderland Private Server.exe'
```

The GUI starts server initialization automatically. Wait for the login-listener
message and `[BattleBackdrop] Loaded Ground.MMG backdrops for 1147 maps.`
The map count can differ with another client data version. Keep the GUI open.
Create an account at `http://localhost:8080/` or through the administration UI.
Configure the Legacy client's server list to connect to `127.0.0.1:6414`.
Do not point it at Gonline's capture relay when testing this server directly.
Use the GUI's safe shutdown when finished.

Upstream currently starts both status and item-mall listeners on 6416; status
starts first, so the later mall listener can log a bind warning. This predates
the backdrop patch and does not prevent login/gameplay on 6414. It is not a
reason to point the game client at a different port.

This change was verified by build and isolated production-assembly tests. No
interactive live login/battle test was performed during this implementation.

## Validation

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/Run-GroundBackdropTests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/Run-GroundBackdropTests.ps1 -GroundPath Data/Ground.MMG
```

The first command uses independent raw-byte fixtures for layout, edges,
overlap order, malformed/truncated archives and missing maps. The second adds
all native-map counts, seven known map positions, actual startup path loading
and preservation of a loaded catalog after missing/corrupt reload attempts.
The native counts deliberately pin this supplied reference archive.

The entire eight-project solution builds. The existing pet-roster regression
also passes. Three older local `.codex-verify` harnesses fail identically on
unmodified upstream `0d16ebb` and this patch: combat action selection has an
outdated `PlayerSkills` test double; capture has a malformed extracted method;
pacing's 36 executable checks pass but a later source assertion rejects an
existing fixed delay. Those baseline harness failures are not presented as
passing validation of this change.

## Upstream review for Gonline

Two upstream commits were added since the prior checkout:

- `0b7e7c6`: experimental-features administration tab/module metrics.
- `0d16ebb`: modern administration UI, server tools, backups, announcements,
  activity logs, logger queue/retention fixes and autosave metrics.

Useful candidates for Gonline are consistent scheduled SQLite backups with
retention, checkpoint duration/slow-save metrics and daily peak-player metrics.
A scheduler could extend Gonline's existing manual administration announcement
API. The WinForms UI is specific to Private Server; the broad full-character
autosave loop should not replace Gonline's dirty-state checkpoints and
transactional inventory/currency policy. Activity logging and queue changes
require their own review rather than direct translation: upstream performs
synchronous chat-file writes and uses an unbounded concurrent log queue.
No unrelated Gonline feature ports were made in this backdrop change.
