# Theme art sources

The seven original PNG boards were created with the built-in `image_gen` tool on 2026-10-20 for this game. Each 1536×1024 board contains one distant environment panorama and six normal monsters plus one boss. [Generation.json](Generation.json) preserves the exact prompt for each board and the original generated file path.

The project uses independent copies in `Assets/Demos/Roguelike/Resources/Tower/Themes/`. The generated PNG bytes remain unchanged. `AtlasLayout.json` records measured top-left rectangles because the generated placements differ from the requested grid. The runtime extracts the main connected silhouette into a cached transparent cutout to exclude tiny neighboring sprite fragments; it preserves every retained RGBA pixel. Panorama sprites use the measured background rectangle, aspect-preserving scaling, and a subdued tint.

Terrain and foreground details are code-native pixel art in `TowerThemeWorld.cs`: seven material palettes, 48ppu stone/ice/wood/moss surfaces, low decoration clusters, and a carved portal. The existing 16 platform patterns and collision surfaces are preserved. No danger-zone or bullet-path preview is introduced.

The `Themes` smoke suite captures seven in-game maps and seven isolated roster galleries. The galleries inspect the assets and do not represent continuous combat. Source atlas identity, 49 unique silhouettes, transparency, complete framing, platform alignment, and theme transitions are checked separately from the movement and boss suites.
