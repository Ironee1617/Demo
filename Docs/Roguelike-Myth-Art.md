# Myth companion pixel art

**Eden update:** indices 100–104 now use five separate pixel sheets with balanced youthful proportions and authored frame animation through `TowerEdenArt`. See [Eden artwork and animation](Roguelike-Eden-Cute.md). The source-atlas and joint-rig notes below remain applicable to the other 45 companions and document the preserved original Eden source. The mesh animation runner now checks 45 companions; use `Tests/Run-TowerEdenReview.ps1` for Eden's 80 pixel poses.

## Source and scope

These original raster assets were generated for this project with OpenAI's built-in ImageGen tool. They are new interpretations of mythological figures, rather than copied game sprites or historical artwork. The files live in `Assets/Demos/Roguelike/Resources/Tower/Myths/`. Preserve each PNG together with its Unity `.meta` file.

Every character has a bipedal humanoid body. Most show human faces; Anubis's face is covered by a full jackal motif mask. Wings, divine symbols, headdresses, masks and equipment identify the source tradition. Animal-associated deities and the zodiac appear as humanoid champions with animal motifs. These fantasy designs are not intended as authoritative religious iconography. The zodiac roster initially covers Rat, Ox, Tiger, Rabbit and Dragon, five of the twelve signs.

## Roster and indices

Columns follow this order, left to right. Art indices continue across each row of this table.

| Atlas | Myth | Art indices | Five companion columns |
| --- | --- | --- | --- |
| `eden.png` | Eden's angels | 100–104 | Michael, Gabriel, Raphael, Uriel, Metatron |
| `olympus.png` | Olympus | 105–109 | Zeus, Poseidon, Hades, Athena, Artemis |
| `asgard.png` | Asgard | 110–114 | Odin, Thor, Loki, Freyja, Tyr |
| `india.png` | Indian mythology | 115–119 | Shiva, Vishnu, Brahma, Ganesha, Hanuman |
| `egypt.png` | Egyptian mythology | 120–124 | Ra, Osiris, Isis, Anubis, Horus |
| `china.png` | Chinese mythology | 125–129 | Sun Wukong, Nezha, Erlang Shen, Guan Yu, Nuwa |
| `korea.png` | Korean mythology | 130–134 | Dangun, Jumong, Hwanung, Princess Bari, Jacheongbi |
| `solomon.png` | Solomon's 72 demons | 135–139 | Bael, Agares, Vassago, Paimon, Asmodeus |
| `ireland.png` | Irish mythology | 140–144 | Cu Chulainn, Fionn mac Cumhaill, Lugh, Morrigan, Dagda |
| `zodiac.png` | Zodiac champions | 145–149 | Rat, Ox, Tiger, Rabbit, Dragon |

## Generation prompt specification

The following normalized specification was used for each atlas, with the corresponding five-character roster above and a separate costume, weapon and color identity for each character:

> Use case: stylized-concept. Asset type: production Unity 2D pixel-art animation atlas, transparent PNG. Create one precisely aligned spritesheet with exactly five columns and six rows, thirty cells total. Each column contains one humanoid myth companion, maintaining the same character down the column. Rows from top to bottom: idle standing, run stride, weapon attack, spell cast, hurt recoil, collapsed death lying sideways. Keep the complete body and weapons inside each cell with generous transparent margins, an aligned baseline and no overlap. Orthographic side-scrolling three-quarter view; all living characters face right. All five characters have human faces and bipedal bodies, including animal-associated deities as human champions with motif headpieces. Detailed 32-bit-era low-resolution pixel art, chunky crisp square pixels, dark one-pixel outlines, restricted palettes, no gradients or smooth painted surfaces. Character height around 64 logical pixels. Maintain consistent scale and distinct costumes, weapons and silhouettes. No text, names, grid lines, borders, background or background shadows. True transparency.

The intended grid has equal cells. Generated output can vary in row spacing, so runtime slicing uses measured boundaries where supplied rather than assuming a perfectly equal grid.

## Import and frame layout

`TowerMythArtImporter` sets Point sampling, Clamp wrapping, no mipmaps, no non-power-of-two resizing, uncompressed color, transparency and a maximum texture dimension of 4096. The committed PNG metadata sets the same values before the editor runs. Atlases are imported as textures; `TowerMythArt` creates sprite rectangles at runtime without destructive source slicing.

The six generated rows are retained as source artwork. Current runtime animation uses the consistent idle artwork through `TowerMythModel`, a layered model of source-pixel strips. Each strip has one anatomical owner. Adjacent armor, a face, a weapon and a robe do not share deforming triangles. Source texture coordinates and body scale stay fixed during state changes. `TowerMythRig` supplies the transforms for hips, knees, feet, shoulders, elbows, head, cloth and the carried weapon; movement, attack, casting, recoil and collapse use those same painted parts.

`RigData.json` supplies measured anatomical landmarks for all 50 characters. Pixel coordinates are relative to the idle cell and use a bottom origin. The sole midpoint is the root, and body height excludes halos, wings and tall ornaments. Source ownership is assigned to a compact face core, the measured weapon, the wider head/hair region, exposed limbs, the torso and back cloth. Weapon capsules and complete tip silhouettes move with the measured carrying hand. Secondary props such as Athena's shield, Bari's bell and Loki's second dagger have separate bounded source masks. Small textured overlaps at cut boundaries and sampled source-texture repair behind moved foreground parts cover joints without adding generic body shapes. Carrying-arm depth follows the measured hand, and front garments occlude the stepping legs. See `Roguelike-Model-Quality.md` for the roster's visual archetypes and source-mask cautions.

Optional `sourceMinX`/`sourceMaxX` bounds remove neighboring-cell fragments from the runtime idle model. A few props cross a source column boundary: the model recovers Gabriel's trumpet bell, Guan Yu's guandao edge and Tiger's sword continuation from bounded adjacent texels and attaches them to the correct weapon. Geometry, UVs and anatomical anchors retain the original cell coordinate system. Source PNGs remain intact.

The current gait uses full travel-matched steps. World movement speed is converted through the actor's actual scale, including the production companion scale of 0.82, so a planted sole counter-travels with its moving root. Walking and running differ in cadence, lift, body response and running flight. Long robes cover the upper legs rather than shortening the physical step. Nezha uses a coherent wheel-hover pose. Grounded hips and legs keep their live phase during attack and casting transitions; upper-body preparation, contact and recovery blend independently. Weapon archetypes distinguish sword, spear, hammer, bow, staff, orb and dual-weapon actions. Support spells release at production combat timing. A zero-time update freezes the pose. The original player and monster animation paths remain available.

`AtlasLayout.json` records measured layouts for all ten atlases. `rowCuts` run from the top of the PNG to the bottom; `columnCuts` run left to right. Optional `rowCutsByColumn` contains seven row boundaries for each of the five columns, in column order, and accounts for individual characters whose effects touch a neighboring row. Coordinates scale with the imported image dimensions. Every row uses the same atlas pixels-per-unit, so a taller spell effect does not shrink the character and a shorter collapsed pose does not enlarge it. Unspecified layouts use equal subdivisions.

For the 1140 × 1380 Eden atlas, alpha projection and visual inspection identified clear horizontal gaps. The measured row cuts are `0, 256, 468, 695, 989, 1196, 1380`; column cuts are `0, 228, 456, 684, 912, 1140`. Some wide attack effects approach adjacent column edges, while the humanoid bodies remain within their columns. Review measured cuts whenever an atlas is regenerated.

Portraits render each companion's idle sprite into a cached transparent 128 × 128 texture with Point sampling. Existing player bone art and monster sprite behavior use their original rendering path.

## Animation regression checks

Build the Windows player with `Tests/Build-TowerDemo.ps1`, then run `Tests/Run-TowerAnimationSmoke.ps1 -Executable <player.exe> -ReportDirectory <output>`. The current `rendered-source-points-v2` suite evaluates 414 poses per companion, 20,700 in total. Actors physically move at production scale against a stationary world floor at 1.5, 3 and 5 world units per second. The suite checks actual rendered sole contacts, alternating stride, clearance, finite topology, fixed source mapping and scale, pause, facing reflection, face/garment/weapon rigidity, visible action excursion, attack/cast recovery and interrupted recovery. Nezha is reported separately as the one hovering character. A zero-time call must preserve every vertex. The runner rejects a build using the obsolete stationary-root test contract.

Face and garment probes use painted points contained by actual rendered triangles; transparent contour points are excluded. A covered core without eligible painted samples is explicitly reported as unmeasured/occluded. Weapon probes sample the actual shaft and tip, and reports retain the worst motion/frame, source coordinates and coverage counts. Foot-contact reports compare world-space displacement of rendered sole vertices with physical root travel, including during moving attacks and casts. A small local stride or a rigid source patch alone is not evidence of natural motion or a complete model.

The suite renders seven representatives: Michael, Athena, Artemis, Zeus, Ganesha, Bari and the player. Its 240-frame sequence covers idle, three movement speeds, moving attack/recovery, moving cast and stopping; a separate 180-frame loop shows one second at each speed. The camera follows the physically moving actors while world meter ticks remain fixed. `Tests/Render-TowerAnimationQA.py <output> --before <baseline>` packages normal-speed and slow-motion GIFs plus a contact sheet without editing source artwork. Review full cycles and all thirteen `-ArtOnly` roster pages for holes, duplicate limbs, detached source fragments, exposed repair patches, weapon completeness, face distortion and garment depth. Numeric checks alone do not establish visual quality. `-MythOnly` and `-CompanionsOnly` exercise selection, rewards and real combat updates separately.

## Historical baseline and current acceptance

The earlier 2026-10-20 fine-grid build passed 50 companions / 13,950 motion samples, the 51-character art gallery / 357 poses / 16 VFX suite, and mythology selection, points and purchases. Combat validation covered all 50 basic attacks and 200 owned skills. Its evidence in `.validation/AnimationQA/FinalValidated` and the earlier `Artifacts/TowerArt/animation-*.gif` files is a historical baseline. It fixed sprite-swap proportion changes and major face/weapon distortions, but its costume-limited strides still skated in world space and Michael's shin compressed during a running bend. Those results do not validate the newer detached model or its full gait.

Current model-quality evidence is `.validation/ModelQuality/Verified/`. Its actual moving captures and all thirteen fresh roster pages were visually reviewed after the final source-ownership corrections. Full bow arms, complete curved blades and spearheads, low soles, moving attacks/casts and return to idle were checked. Bari's outer front dress also stays continuous in anticipation frame 098, which exposed a split missed by contact-pose-only review. No severe remaining silhouette blocker was found in that review. Small source-edge flecks and the flat appearance of a single painted viewpoint remain limitations.

The completed `.validation/ModelQuality/Verified/animation-report.json` reports `success: true` and `naturalMotion: true` for all 50 companions / 20,700 poses, including 49 grounded actors and Nezha's hover. It finished at `2026-10-20T02:53:03Z` with no failures. The same candidate passed the 51-character art suite / 357 poses / 16 VFX. This supersedes the rejected `Visual`, `Textured`, `Seams`, `Polished`, `Final` and `Release` model-quality captures; their failed reports remain diagnostic history. The ten original PNGs have separately been verified by SHA-256 against the generated source files; `Artifacts/TowerArt/myth-source-preservation.json` records that preservation check.
