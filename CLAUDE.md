# Tales of Pirates Unity port

The project aims to port D3D9 C++ game Tales of Pirates to Unity.

## Unity editor

**Unity CLI MCP** drives the running editor. It does not start one, so check firstwith `unity status --json`.
It exposes one tool per editor operation, forwarded from the `com.unity.pipeline` package.
Mutating tools take `dry_run`; destructive ones need `confirm`. Builds, tests, bakes and package
changes return at once and report progress through a matching `*_status` tool.

## Converting content

`tools/build.ps1` builds the converter, `tools/convert.ps1` (or `tools/convert.cmd`, which needs no
PowerShell knowledge) runs it, and `tools/README.md` lists the arguments. The same program is behind
**Tools → Content Converter** in the editor: scan for client roots, load a client's families and
units, pick one and convert. It writes the content folder into the scene's `MapPreview`, which owns
the folder both play mode and **Tools → Map Region Viewer** read.

A conversion is per family, and a converted tree only loads if it holds what a reader opens first:

- `table` - `tables/*.json`; everything needs it, and naming `map` pulls it in.
- `map` - terrain and water of one map, plus the scene objects that map places, which it names
  by their sceneobjinfo id (`--no-objects` leaves them out).
- `scene` - the models standing on a map (`models/scene/*.glb` + `textures/scene/*.png`).
- `character`, `item` - converted into `rigs/` and `models/`, but nothing in the client reads them yet.

So `--kind map --unit 32` is one map with the objects standing on it, while `--kinds map` converts
every map in the client. The map region window deliberately draws ground and water only: scene
objects load through GLTFast, which cannot run outside play mode.

Where a hero can be put on a converted map is a listing rather than a conversion:
`--list spawns --kind map --unit <name|id>` prints the map's own start from mapinfo and then every
object it places, with map coordinates, the tile height under each and whether that is land. It
reads the tree, so it lists what the runtime will load.

## Playing

`HeroController` walks the hero with WASD under a `FollowCamera`. `CameraMode`, added to the same
camera, switches to free flight with Tab and back: WASD flies, Q/E go down and up, the right mouse
button looks, the wheel changes speed, shift sprints. While flying it disables the follow camera and
the hero's own controls, and points the preview's streaming at the camera - a camera flown away from
the hero would otherwise stare at an empty scene, because chunks only build around the preview's focus.

## Lessons learned

- The map is world space. Terrain vertices, placements, the hero and the camera are all world
  coordinates, and `Tools → Map Region Viewer` builds at the origin - so the object a `MapPreview`
  sits on must not move the map, and `MapInstance` forces its group to an identity world transform.
  A preview object dragged in the editor otherwise takes the whole runtime map with it, and play mode
  shows an empty scene wherever the hero stands. Map units and world units differ: world is
  `(-mapX, height, mapY)`, which is what `--list spawns` prints.
- Each Unity package under `src/` has two project files. The `src/` one targets `netstandard2.1` with the NuGet
  reference set and no defines. Unity generates a second one from the `.asmdef`, with `v4.7.1`, Unity's reference
  set and about 150 `UNITY_*` defines. An LSP loads one of the two, so its diagnostics can miss what Unity
  reports. Check the editor console. `dotnet build` does build those generated projects, which type-checks
  Unity code without the editor.
- A `save_path` on a capture command always resolves under `Assets/`, even when absolute. The result reports
  success and a rewritten `savedPath`, and the PNG becomes an asset. Use `include_inline_image` instead.
- Unity pins C# 9, and the Unity packages set `LangVersion 9` to match. The other `src/` projects are `net10.0`
  with no `LangVersion`, so they get the newest C#. Code moved from one of those into a Unity package can stop
  compiling.
