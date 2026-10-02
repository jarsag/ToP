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
- `character`, `item` - a character becomes a rig plus one model per body part and piece of equipment
  (`rigs/`, `models/character/`); the hero wears them, and a mark on the map can be drawn from an item.

So `--kind map --unit 32` is one map with the objects standing on it, while `--kinds map` converts
every map in the client. The map region window deliberately draws ground and water only: scene
objects load through GLTFast, which cannot run outside play mode.

Where a hero can be put on a converted map is a listing rather than a conversion:
`--list spawns --kind map --unit <name|id>` prints the map's own start from mapinfo and then every
object it places, with map coordinates, the tile height under each and whether that is land. It
reads the tree, so it lists what the runtime will load.

## Playing

`HeroController` walks the hero where the player points: holding the left mouse button steers, letting
it go leaves a mark on the ground at the destination, and the hero walks there along the map's height
field. The keyboard belongs to the camera. `CameraMode`, added to the camera, switches with Tab between
watching the hero and flying free: `IsometricCamera` keeps him in view and is preferred when the scene
has one, `FollowCamera` is the fallback, and `FlyCamera` is the free one - WASD, Q/E down and up, the
right mouse button looks, the wheel changes speed, shift sprints. While flying, the hero's controls are
switched off and the preview's streaming follows the camera: a camera flown away from the hero would
otherwise stare at an empty scene, because chunks only build around the preview's focus.

A click does not raycast against geometry - there is none, the ground is a height field - so `MapRay`
walks the ray forward and compares it with `MapData.HeightAt`, which lands on ground whose chunks are
still streaming in. The mark is a `DestinationMarker` (it turns and hovers over the point it was put
at), drawn from a prefab when one is assigned and made on the spot otherwise: the model its marker
model names, which `ContentModel` loads out of the converted tree, or a flat disc when no model is
named.

`HeroModel`, added to the hero, dresses it in a converted character. A player character converts as
parts: a rig (`rigs/<model>.glb`) that is a skeleton and its clips and draws nothing, and one file per
body part and piece of equipment (`models/character/<fileId>.glb`), each carrying its own copy of the
same skeleton and no clips of its own. The two copies have identical bones in an identical rest pose,
so each part is handed the rig's clips and animates itself - no rebinding - and `HeroController.Speed`
paces the move clip. Clip names are `{model}_{nn}_{action}`, from `Naming.ActionClip`.

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
