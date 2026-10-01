# Converter scripts

Two wrappers over `dotnet`, so the converter is one command from any folder,
plus the same thing inside the editor: **Tools → Content Converter**.

The converter itself is `src/Top.Conversion.Cli`. It reads the original client
and writes the tree the Unity client reads (`artifacts/content` by default).
Nothing here reimplements it - the scripts and the editor window start the same
program, and the editor window is a front end to it.

## Build

```powershell
tools\build.ps1                       # Debug
tools\build.ps1 -Configuration Release -Test
```

Output lands in `artifacts/bin`, which is where the editor window looks for it.

## Run

```powershell
tools\convert.ps1                     # the wizard: pick a client, a family, a unit
tools\convert.ps1 -Clients            # client roots found around the repository
tools\convert.ps1 -Clients -Json      # the same, as JSON (what the window reads)
tools\convert.ps1 -Catalog -Source 'C:\work\TalesOfPirateDX9\Client'
tools\convert.ps1 -Source 'C:\work\TalesOfPirateDX9\Client' -Kind map -Unit PKmap
tools\convert.ps1 -Source 'C:\work\TalesOfPirateDX9\Client' -Kind map -Unit 32
tools\convert.ps1 -Source 'C:\work\TalesOfPirateDX9\Client' -Kinds map,scene -Build
```

`tools\convert.cmd` takes the same arguments and needs no PowerShell knowledge;
with none at all it opens the wizard.

One unit is named by its id where the family numbers its units (character, item,
scene) and by its name otherwise. A map answers to both: its mapinfo id, which is
the number `MapPreview` loads it by in the scene, and the name of its file, which
is what the converter keys it by - so `-Kind map -Unit 32` and `-Kind map -Unit
PKmap` are the same map.

Relative paths resolve against the repository root, so `-Out artifacts/content`
means the same folder here as it does in Unity. `-Kind` with `-Unit` converts
one unit plus whatever that unit cannot be read without (a map pulls the tables
with it); `-Kinds` converts whole families.

## Which families to convert

| Family | What it writes | Needed for |
| --- | --- | --- |
| `table` | `tables/*.json` | everything - a reader opens the tables first |
| `map` | `maps/*.map` | the terrain and water of a map, plus the objects standing on it (pulls `table` itself) |
| `scene` | `models/scene/*.glb`, `textures/scene/*.png` | the objects standing on a map |
| `character` | `rigs/*.glb`, `models/character/*.glb` | nothing the client reads yet |
| `item` | `models/item/*.glb` | nothing the client reads yet |

A map converted on its own brings the objects it places and no others: the map
names them by their sceneobjinfo id, so `-Kind map -Unit 32` converts the map
plus exactly the models standing on it. `-NoObjects` leaves them out, and
`-Kinds scene` converts the whole family instead.

So a map with its objects is a single `map`, for example:

```powershell
tools\convert.ps1 -Source 'C:\work\TalesOfPirateDX9\Client' -Kind map -Unit 32
```

## Spawn points of a map

A converted map knows where its own start is and where everything it places
stands, which is the list of places a hero can be dropped:

```powershell
tools\convert.ps1 -List spawns -Kind map -Unit 32
tools\convert.ps1 -List spawns -Kind map -Unit 32 -Json
```

```
map 32 PKmap (PKmap) -> maps/pkmap.map
  start  map 50,50   world (-50, 50)   height 0.7
      #  kind     object     map x     map y   world x   world z   height  facing
      1  model       316      23.6      23.9     -23.6      23.9      1.2      75
  a tile is a metre; world (-map x, height, map y) is what a transform takes
```

Both spaces are printed, because they are not the same numbers: the client's map
coordinates run one way and the scene's world coordinates the other, and world is
`(-map x, height, map y)`. So a point from this list goes into a transform as
`(-mapX, height, mapY)`, and the `WorldX`/`WorldZ` fields in the JSON are exactly
that. A tile is a metre.

It reads the converted tree rather than the client, so the list is exactly what
the runtime loads - a placement the converter dropped is not offered. `Land` in
the JSON says whether the tile is above the water line the client draws.

A map the table names but the tree does not hold yet prints `not in the tree yet
- convert it first`; a name the table does not know exits with 1.

## In the editor

**Tools → Content Converter**: scan for a client, load its catalog, pick a
family and a unit, and convert. The window writes the content folder into the
`MapPreview` of the open scene, so play mode reads what was just written, and
the same folder is what **Tools → Map Region Viewer** draws from.

**Tools → Map Region Viewer** builds the terrain and water of a region without
entering play mode. It deliberately draws no scene objects: those load through
GLTFast, which cannot run outside play mode.
