# Myth model and motion quality

This is the visual design and acceptance reference for the layered companion-model pass. It describes the intended result; runtime reports and captured frames establish which checks have passed. Preserve all fifty existing faces, costumes, palettes and identifying props. Original PNG files and their Unity metadata remain source assets.

## Model construction

- Give source pixels one visible owner at rest: back cloth/hair/wings, rear limb, torso, head, front limb, held object or front cloth. Do not leave an original painted limb or weapon behind a moving copy.
- Keep head and held objects rigid. Source-textured overlaps cover cut boundaries; narrow waist, knee and elbow bands share the adjacent joint mappings smoothly. Do not blend a face, blade, ornament or full garment into a moving limb.
- Torso underpainting continues nearby source texture behind removed arm/weapon pixels inside the original character silhouette. Joint repair uses painted overlap only. Do not add procedural socket circles, flat row-color fills or a new exposed body shape.
- Keep wings and long hair out of arm corridors. Keep skirts and capes out of leg corridors. Exposed feet may travel behind a stable garment layer.
- Keep ground shadows at the world contact plane. Mirroring a character must preserve its intended part order and must not duplicate limbs.
- Use consistent logical pixel density and outline contrast at actual gameplay scale. Extra texture resolution alone does not improve readable faces, hands or weapons.

## Movement acceptance

The prior gait multiplied the complete sole trajectory by `StrideScale`, including planted-foot counter-travel. Before the cadence cap, its local stance velocity is `-speed * StrideScale`. Production companions additionally scale their art by `0.82`, while formation movement supplies world speed. Thus the old production stance slip was approximately `speed * (1 - 0.82 * StrideScale)`: about 59% of travel for Michael's 0.50 scale and 85% for Rabbit's 0.18 scale.

World-space foot locking must account for the real art transform, facing and actual root travel. Clothing response can stay small independently of the planted sole. Choose stride and cadence from reachable leg length; release a plant before overextension instead of compressing the shin or sliding throughout stance.

Use a moving parent fixture at the production `0.82` scale. Follow each planted sole across its entire stance interval; its world drift should stay around one logical gameplay pixel. Check starts, stops, reversal, catch-up, attack entry and recovery. A stationary gallery or changed transform hash cannot establish this.

Nezha (126) is a deliberate exception: preserve both flame wheels and use a hover/wheel movement cycle. His anatomical soles sit above the wheel bottoms. A normal alternating biped step compresses or splits the wheels.

## Action acceptance

Basic attacks follow the visible held object, even when the combat role is Archer, Mage or Healer. Casts may use the appropriate spell/support motion. Only Artemis (109) and Jumong (131) visibly hold bows; do not make other ranged characters pull an imaginary bowstring.

Each action needs a readable anticipation, contact silhouette, follow-through and recovery. Weight transfer should involve pelvis, torso and the free arm. A short forward hand nudge with a nearly stationary body is insufficient for a dynamic melee attack. Preserve full weapon contours through the complete swing, including decorative tips, guards, tied cloth and both ends of a staff.

Bow aim raises the full carrying arm to aim height and draws the opposite hand toward the cheek. Check both whole-arm silhouettes: a palm reaching the face while the original forearm remains lowered does not meet this requirement. Explicit source masks and actual shoulder/elbow/fist pivots for 109 and 131 are listed below.

Cape, hair and wings should trail the main body with separate sections and delayed settling. They should not move as one rigid cardboard sheet or share a leg's rotation.

## Roster archetypes and silhouette cautions

These are visual action archetypes, independent of gameplay role. `Orb` includes a held bolt, book, discus or magical focus. `Staff melee` is a physical two-handed staff action; ordinary `Staff` is a casting/support action. `Hammer` includes axes, maces and the heavy club. The carrying image hand comes from `RigData.weaponHand`; preserve its spatial left/right meaning.

| Art | Companion | Combat role | Visual basic action | Part/depth caution |
| --- | --- | --- | --- | --- |
| 100 | Michael | Vanguard | Sword | Left-image-hand sword; wings behind arms and cape. |
| 101 | Gabriel | Archer | Staff/support: trumpet | Raise/recoil trumpet; no bowstring. Preserve full bell. |
| 102 | Raphael | Healer | Staff | White wing and robe must remain distinct from arm. |
| 103 | Uriel | Mage | Sword | Left-image-hand sword; retain halo and wing silhouette. |
| 104 | Metatron | Mage | Orb: book | Rigid book and face; layered wings extend on both sides. |
| 105 | Zeus | Mage | Orb: bolt | Bolt follows casting hand; long kilt stays independent of feet. |
| 106 | Poseidon | Vanguard | Spear | Full three-prong head and long shaft remain rigid. |
| 107 | Hades | Mage | Staff | Preserve green trident ornament; cloak stays behind legs. |
| 108 | Athena | Vanguard | Spear + shield | Shield needs a separate rigid left-arm owner. |
| 109 | Artemis | Archer | Bow | Front bow arm extends; opposite hand draws toward face. |
| 110 | Odin | Mage | Spear | Preserve long spear tip, beard and hood. |
| 111 | Thor | Vanguard | Hammer | Hammer is carried by the left image hand. |
| 112 | Loki | Mage | Dual | Both dagger silhouettes must follow their respective hands. |
| 113 | Freyja | Healer | Staff | Long blond hair and red dress trail separately. |
| 114 | Tyr | Vanguard | Sword | Left-image-hand sword; cape remains behind weapon arm. |
| 115 | Shiva | Vanguard | Spear | Bare torso and tiger cloth must not acquire invented armor fills. |
| 116 | Vishnu | Archer | Orb: discus | Disc throw/release; no bowstring. |
| 117 | Brahma | Mage | Staff | Lotus and entire gold finial are one ornament. |
| 118 | Ganesha | Healer | Hammer: axe | Preserve complete head motif and curved axe blade. |
| 119 | Hanuman | Vanguard | Hammer: mace | Entire spherical head, finial and collar remain rigid. |
| 120 | Ra | Mage | Staff | Solar halo belongs to head/back layer, not the scepter. |
| 121 | Osiris | Healer | Staff | Tall crown remains attached to head; robe hides upper legs. |
| 122 | Isis | Healer | Staff | Winged ankh includes its top loop and both blue wings. |
| 123 | Anubis | Vanguard | Sword: khopesh | Gold curved blade is unstrung; do not animate it as a bow. |
| 124 | Horus | Archer | Spear | Ranged release uses spear/light projection, not a bow. |
| 125 | Sun Wukong | Vanguard | Staff melee | Both gold staff caps stay rigid; two-hand support pose. |
| 126 | Nezha | Vanguard | Spear + hover | Preserve both complete flame wheels; no alternating walking stride. |
| 127 | Erlang Shen | Archer | Spear | Preserve all three spear points; ranged role does not imply bow. |
| 128 | Guan Yu | Vanguard | Spear: slashing polearm | Recover missing guandao continuation from adjacent source cell. |
| 129 | Nuwa | Healer | Staff | Flowing veil/hair stay behind arms; remove foreign blade pixels. |
| 130 | Dangun | Vanguard | Sword | Left-image-hand sword passes in front of the robe. |
| 131 | Jumong | Archer | Bow | Tall helmet feathers remain head/back detail. |
| 132 | Hwanung | Mage | Staff | Blue ribbons remain attached to staff; long robe stays stable. |
| 133 | Princess Bari | Healer | Staff + bell | Flower staff and left-hand bell need distinct rigid owners. |
| 134 | Jacheongbi | Archer | Staff + sickle | Grain staff and left-hand sickle; no imaginary bow. |
| 135 | Bael | Mage | Sword | Left-image-hand red blade uses a physical basic swing. |
| 136 | Agares | Mage | Staff | Crocodile head, gem and serpent collar form one full ornament. |
| 137 | Vassago | Archer | Orb | Orb release/recoil; no bowstring. |
| 138 | Paimon | Mage | Staff | Crown, long hair and blue robe retain independent owners. |
| 139 | Asmodeus | Vanguard | Spear | Visual weapon is a fire trident despite the skill's sword wording. |
| 140 | Cu Chulainn | Vanguard | Spear | Red blade and both tied red strips belong to weapon. |
| 141 | Fionn mac Cumhaill | Archer | Sword | Left-image-hand sword; ranged role does not imply bow. |
| 142 | Lugh | Archer | Spear | Long spear/light release; white cloak remains behind legs. |
| 143 | Morrigan | Mage | Sword | Left-image-hand blade; feather cloak and hair trail behind torso. |
| 144 | Dagda | Healer | Hammer: club | Club follows right hand; gold harp stays at the belt. |
| 145 | Rat Champion | Vanguard | Dual | Keep both knives attached to their own hands. |
| 146 | Ox Champion | Vanguard | Hammer: axe | Wide axe head stays rigid; horns stay attached to head. |
| 147 | Tiger Champion | Vanguard | Sword | Recover missing blade continuation from Rabbit's source cell. |
| 148 | Rabbit Champion | Healer | Staff | Stable floor-length dress; ears and face stay rigid. |
| 149 | Dragon Champion | Mage | Spear | Preserve full spear head, antlers and trailing blue cape. |

## Secondary-prop registration reference

All coordinates below are source **top-origin** pixels inside the original untrimmed idle cell. Capsule width is the full diameter. These measurements describe source ownership, not a new anatomical hand pivot. Keep the original source-coordinate origin when constructing the part.

| Art | Secondary prop | Start | End | Full width | Owner |
| --- | --- | --- | --- | --- | --- |
| 112 | Loki left dagger | (97,195) | (123,239) | 20 | Left forearm |
| 145 | Rat left dagger | (96,174) | (65,233) | 22 | Left forearm |
| 133 | Bari bell | Bounded polygon below | Bounded polygon below | — | Left forearm |
| 134 | Jacheongbi sickle handle | (82,178) | (55,188) | 16 | Left forearm |
| 134 | Jacheongbi sickle belly | (55,188) | (34,177) | 18 | Left forearm |
| 134 | Jacheongbi sickle tip | (34,177) | (27,161) | 12 | Left forearm |

Jacheongbi's three capsules form one bounded curved prop. A broad straight capsule catches neighboring hair or sleeve pixels. Athena's shield is better represented by a source ellipse centered at (79,173), radii (40,47), covering the approximately x=42..115, y=128..214 silhouette. Assign this shield before leg ownership: its lower edge reaches the hem/leg region. A generic ellipse centered near the higher left hand omits the shield's lower third.

Bari's bell uses the top-origin polygon `(113,175), (120,175), (121,181), (124,187), (127,192), (130,197), (129,202), (125,205), (109,206), (102,203), (100,198), (104,192), (108,187), (110,181)`. A 26-pixel-wide capsule extending to y=202 also captures white garment below the bell through y=215, leaving a rectangular hole when the hand moves. Her actual fist is near top-origin `(116,169)`, approximately bottom-origin y=111 in the 280-pixel idle cell. Continue the white/pink garment behind the removed bell from nearby source cloth; the required outer region x=100..130 lies outside a generic narrow torso repair.

Her front dress also flares far left of a central torso rectangle. Keep the full white/pink front garment on the pelvis using the top-origin polygon `(129,167), (153,167), (168,208), (176,241), (183,258), (176,266), (166,270), (145,267), (127,270), (109,270), (95,265), (80,265), (67,261), (58,253), (62,245), (75,232), (87,218), (101,202), (111,183)`, after bell/weapon ownership. Leaving its x=58..100 outer hem on the delayed back-cloth layer opens a large triangular slit during anticipation even when the contact pose looks closed. Inspect preview frame 098 as well as 108 and 169.

## Primary-prop source corrections

Athena's lower spear follows approximately top-origin `(142,257), (148,185), (153,145), (160,80)`. A capsule starting at bottom-origin `(155,12)` misses the lower shaft; the measured base is approximately `(142,13)`. Erlang's three-pronged head spans x=186..258, top y=30..115. Its complete localized mask needs an ellipse near bottom-origin `(225,166)` with radii `(47,63)`, including diagonal outer corners. Keep genuine multipart weapon sections when hand occlusion separates their painted alpha components.

Michael's bright blade is surrounded by a broad painted flame halo. A narrow blade capsule, especially one narrowed again at a torso crossing, leaves orange flame pixels attached to the pelvis or cloth. The full source top-origin polygon is `(100,153), (111,148), (122,143), (126,147), (123,154), (132,158), (145,160), (152,168), (160,172), (168,183), (175,186), (177,179), (184,188), (184,196), (194,204), (213,222), (215,230), (201,232), (179,224), (156,211), (137,203), (120,193), (109,185), (100,177), (94,172), (98,163)`. Some orange glow is baked over the original armor and tabard, so detaching the complete effect also requires a narrow textured continuation of the body behind it. A rigid bright core alone does not prove that the full weapon silhouette is correctly owned.

The outer right flame tongue extends beyond that polygon. The bounded top-origin rectangle x=176..218, y=176..234 contains the remaining flame and lies beyond the body at that height. Alpha-gated inclusion of that rectangle avoids leaving thin orange shards on the cloth. An independent source connectivity check found the polygon's 4,058 painted pixels formed one component; component pruning did not cause that particular residual.

Morrigan's sword has a curved lower belly outside a straight 15-pixel capsule. Its top-origin blade polygon is `(81,198), (92,208), (105,217), (117,229), (133,237), (146,243), (158,243), (160,247), (150,250), (136,250), (121,245), (107,239), (95,231), (83,221), (74,213), (70,207)`. Keep the handle capsule as well. Moving only the upper edge leaves an apparent second silver blade at the original waist.

## Bow-arm registration

The source view is asymmetric: a generic shoulder offset from the chest center can lie well inside the chest rather than at the visible arm joint. The two real bow users need explicit left-image arm registration so a full arm reaches the draw pose instead of moving a few torso pixels while leaving the original lowered arm behind.

| Art | Idle height | Shoulder (bottom origin) | Elbow (bottom origin) | Fist (bottom origin) |
| --- | --- | --- | --- | --- |
| 109 Artemis | 270 | (98,144) | (86,125) | (85,93) |
| 131 Jumong | 280 | (96,150) | (84,125) | (87,95) |

Artemis's top-origin upper-arm polygon is `(89,120), (104,119), (109,125), (104,133), (98,140), (89,145), (80,140), (83,129)`; her forearm is `(82,138), (94,139), (96,149), (92,162), (91,167), (92,176), (89,184), (84,187), (80,184), (77,178), (78,170), (79,159), (79,148)`. Jumong's upper-arm polygon is `(85,111), (99,111), (110,119), (107,132), (101,145), (91,150), (77,146), (73,139), (77,126)`; his forearm is `(77,148), (91,149), (95,163), (92,177), (97,182), (97,189), (90,194), (81,192), (79,184), (75,177), (75,163)`. These source masks include armor, bracer and complete fist while leaving quiver, hair and cape behind.

## Sole ownership near grounded props

A narrow shin corridor can omit the toe, and a staff capsule ending inside a shoe can steal that toe before leg classification. The following bounded source ellipses restore complete low soles for four measured robe users. Coordinates and radii are top-origin source pixels. Keep the mask below the measured hem and assign these soles before the weapon where they overlap; do not widen a general leg corridor into the skirt.

| Art | Left sole center / radii | Right sole center / radii |
| --- | --- | --- |
| 102 Raphael | (73,242) / (15,9) | (141,236) / (22,8) |
| 117 Brahma | (85,271) / (16,10) | (151,269) / (22,9) |
| 122 Isis | (91,289) / (13,8) | (144,290) / (19,7) |
| 132 Hwanung | (86,267) / (15,8) | (157,267) / (24,8) |

Raphael's painted staff base is near top-origin `(143,228)` and Hwanung's near `(158,256)`. Their earlier endpoints at `(143,241)` and `(164,269)` overlapped the right shoe, leaving apparent gold fragments behind during a step. Both source ownership and physical foot contact need validation after such a correction.

## Source-continuation recovery

The original atlases contain a few props that cross an intended column boundary. Runtime pruning removed those disconnected foreign fragments from the neighboring character. A layered weapon can recover them for the correct owner without changing the PNG.

The following bounds are inclusive and use source **top-origin coordinates inside the next character's idle cell**. They were measured as 8-connected visible-alpha components with alpha greater than 24. Every idle row starts at source y=0, so no row offset is needed. A one-pixel fringe margin is safe where the neighboring body remains excluded.

| Owner | Adjacent idle cell | Inclusive fragment x | Inclusive fragment y | Owner-local continuation x |
| --- | --- | --- | --- | --- |
| Guan Yu 128 | Nuwa 129 | 0..33 | 179..227 | 263..296 |
| Tiger 147 | Rabbit 148 | 0..15 | 216..232 | 262..277 |
| Gabriel 101 | Raphael 102 | 0..5 | 165..184 | 228..233 |

Nuwa's real connected body begins at x=55; Rabbit's begins at x=27; Raphael's begins at x=21. Tiger's idle height is 265 and Rabbit's is 259, but their common top origin makes the y bounds above directly compatible. Sample only the isolated fragment for the owner's weapon layer; do not widen the entire body crop into the neighbor.

The six safe foreign-fragment clips remain Raphael 102 x=12..213, Nuwa 129 x=44..262, Paimon 138 x=0..213, Fionn 141 x=0..200, Morrigan 143 x=0..190 and Rabbit 148 x=21..263. Their anatomy and UV coordinates remain relative to the original untrimmed idle cell.

## Visual review fixtures

Review actual gameplay scale and an enlarged diagnostic view. Useful representatives are Michael 100 (wing/cape/sword), Athena 108 (shield/polearm), Hanuman 119 (bare torso/heavy object), Nezha 126 (hover wheels), Guan Yu 128 (long recovered blade), Rat 145 (dual weapons) and Rabbit 148 (long robe).

At rest, compare against the original silhouette: no new body lumps, missing decorations, seams or duplicate source limbs. During movement and a full attack/cast cycle, inspect for exposed holes, old limbs left behind, detached props, stretched faces, bent rigid objects, skirt fans, shin compression and incorrect layer order. Compare exact previously failing frames and inspect full cycles rather than accepting a single successful contact pose.

The construction retains the source's single painted viewpoint. It articulates those surfaces in that view; it does not supply newly painted backsides or alternate viewing directions. Small source-texture continuations cover exposed joints and occluded costume regions. Check their visual limits at the intended gameplay scale, and do not describe a passing motion report as proof that every newly exposed surface is painted correctly.

## Accepted candidate evidence

`.validation/ModelQuality/Verified/animation-report.json` passed all 50 companions / 20,700 motion samples at `2026-10-20T02:53:03Z`, with 49 grounded actors and one hovering actor. The same candidate passed 51 characters / 357 gallery poses / 16 VFX. Actual representative moving frames, including Bari's previously failing anticipation frame 098, and all thirteen newly rendered `Artifacts/TowerDemo/art-motion-*.png` pages were reviewed. The full front dress stays closed, complete weapon contours travel together, bow arms reach the aim/draw pose and each of the fifty source identities remains readable. No severe silhouette regression was found in that review.

This accepts the source-preserving layered pass. Small source-edge flecks and a flat appearance during large turns or knee bends remain visible limitations of the single painted viewpoint. The ten source PNG hashes match the original generated files, as recorded in `Artifacts/TowerArt/myth-source-preservation.json`.

## Locomotion transition corrections — 2026-10-20

The stationary and steady-travel checks above did not cover every locomotion boundary. The new fixture exercises all sixteen original authored rigs (indices 0–14 and 16) and all fifty companion rigs at production scale: start, stop, jump, apex, landing, dash entry, dash exit, hurt and reversal. It captures the actual rendered vertices on the first 60 Hz frame of each change and checks that a zero-time update leaves the pose unchanged. Intentional left/right mirroring is normalized before comparing articulation.

`Artifacts/TowerRig/Baseline/transitions.json` records 60 of 66 actors exceeding the new 0.18-body-height first-frame limit. The original hero changed by 1.290 body heights at dash entry, 1.229 at landing and 0.753 at the jump apex; Clockwork Blades changed by 1.865 body heights when stopping. The companion maximum was Tyr's 0.297-body-height reversal. These are rendered-point displacements, including held objects, rather than a change in the character's physical root.

The original rigs now blend their locomotion layer over 120 ms. Jump/fall poses interpolate across vertical speed zero, hurt and landing recoil ease in, and sampled angular channels follow their shortest arc. The physical root is no longer snapped independently to whole pixels while the camera moves continuously. All original weighted geometry and textures remain the same.

Companions now retain their gait phase through reversal, rebase contact trajectories into the mirrored view, and blend airborne arms, knee-pole selection and vertical lean. Acceleration lean is damped independently of physical travel. A sequence-dependent dash exit exposed a separate support-height discontinuity: shortening the next step could release a deep leg-reach correction and raise the body roughly 0.32 local units in one frame. The rig now limits that upward recovery to 3 local units per second while immediately respecting any lower reach ceiling. This leaves sole targets and leg lengths intact.

The first-frame transition limit supplements the existing 20,700-sample companion suite. It does not replace checks for complete weapon contours, rigid faces, garment coverage, world-space foot contact, action recovery or actual captured appearance. The underlying single-view artwork and its visual limits remain unchanged.

`Artifacts/TowerRig/Final/animation-report.json` passed at `2026-10-20T04:25:53Z`: all 66 rigs / 594 transition checks and all 50 companions / 20,700 motion samples, with no failures or errors. The maximum first-frame transition is now 0.170 body heights. Hero dash entry, landing and apex measure 0.069, 0.067 and 0.073 respectively; Artemis and Rat Champion's previously failing dash exits now measure 0.076 and 0.075. The largest action contact drift remains 0.00387 world units, with maximum face and weapon edge strain below 0.0007%. Final captured frames 098, 108 and 208 were reviewed against the previous candidate: complete held objects, the bow silhouette, Bari's front dress and the hero's articulated weapon remain intact through anticipation, contact and stopping.

## Source fragment and limb follow-up — 2026-10-20

Later enlarged attack captures exposed defects that the transition report does not measure: Zeus and Ganesha's painted arms crossed generic ownership corridors, and moving those arms exposed missing cape surfaces. All fifty companions now have separately measured left/right shoulder, elbow, hand and arm silhouettes. Ownership is read directly through `SourcePixelOwner`; transparent pixels return 255 rather than being inferred from a nearby bone. Source PNGs and their metadata remain unchanged. The actual ten-pose galleries in `Artifacts/TowerLimbReview` are the appearance evidence for this follow-up; the earlier numeric acceptance is not a substitute for reviewing them.

The full fifty-character alpha-component audit is recorded in `Artifacts/TowerLimbReview/Components/source-fragment-audit.json`. All fifty primary connected character components survive their bounded source clips. Nine existing or corrected crops exclude 3,755 visible pixels belonging to neighboring atlas columns. Poseidon 106 now ends at exclusive x=212, removing the purple Hades-cape fragment at x=220..229; Lugh 142 ends at exclusive x=205, removing the neighboring cape scraps at x=222..229. Legitimate disconnected ornaments and recovered weapon continuations remain intact. Portrait sprites apply the same bounds so removed fragments cannot return in summon cards.

Michael 100 and Uriel 103 use small, individually registered patches from the original Eden cast row (top y=695) to continue costume hidden by their idle flames. Coverage is restricted to the old weapon ownership inside measured destination polygons. Michael's patches restore his white tabard, two separately registered thigh surfaces and blue cape; Uriel's restore front cloth and gold trim, without claiming to reconstruct unseen thigh armor. These are static texture continuations bound to the existing skeleton. They do not swap animation frames or alter the original atlases.

Foot ownership 9/12 now uses the foot bone rigidly; only the shin blends into the separately measured ankle. Joint overlaps use a narrow ankle cuff and exclude the toes and sole, which previously could remain behind as a second shin-bound shoe. At the upper leg cut, a separate body weight joins the thigh/shin to the same pelvis/chest transform as the adjacent painted garment without replacing the existing knee and ankle weights. At this stage, ankle connection, hem coverage and contact still awaited the later gait and appearance review recorded below.

Zeus 105 and Nuwa 129 additionally use explicit leg silhouettes. Zeus's visible greave begins at top y=207, above the former hem at y=228; the measured mask permits a bottom-origin hem of 63 without assigning his white front robe to the leg. Nuwa's front thigh lies across the nearest-foot bisector, so her measured image-left leg takes precedence over the rear leg and generic leg corridors are disabled outside both polygons. Her painted knee bends register at bottom-origin `(158,59)` and `(164,50)`. The green drapery stays outside those leg surfaces.

The inner edge of cloth ownership 13 now matches the adjacent body transform within 0.055 world units of the generic pelvis boundary. This closes the straight split visible in Raphael 102's walking robe while preserving cloth motion outside that band. Static sprite, portrait and material caches also reject destroyed Unity objects; the art caches reset on subsystem registration. This prevents a play-session exit followed by an editor capture from reusing a destroyed idle sprite or material.

The enlarged follow-up gallery exposed a second kind of apparent attachment: opaque dark source strokes were left outside a measured moving silhouette. Zeus's right sole pixel `(140,258)` remained on pelvis ownership 0 while the colored shoe moved, and his actual forearm pixel `(156,143)` remained on cloth 13. Full source-alpha contours, including the dark stroke and fringe, are required; a hand/sole center probe alone does not catch this error.

Horizontal mesh seams had a separate construction cause. Each scanline previously formed one full-width quad, while arm and cloth skin weights vary nonlinearly across its width. Neighboring rows of different widths therefore interpolated shared positions differently. The measured quad-center error reached 0.0845 world units for Metatron 104's cloth during casting. Deforming surfaces now share a fixed grid every four source pixels along X; head, rigid soles and held props retain their rigid strips. UVs still sample the same original pixels.

Weapon underpaint now accepts different adjacent costume owners and can use visible costume above and below a diagonal cut. It still requires source costume to bracket the hidden region and does not bridge two separate legs. Michael and Uriel also have rear cape, skirt and wing continuations from the original cast painting. Underpaint uses the same rear-to-front ordering as the model, so a rear donor cannot overwrite a front tabard or thigh.

`Artifacts/TowerLimbReview/FinalV2/100.png`, `102.png` and `103.png` were reviewed after these changes. The large diagonal cutouts behind the flames, the horizontal cape/wing cracks and Raphael's long vertical robe split are closed in these captured poses. The four-pixel grid reduced the measured quad-center approximation error for 100/103/104 from 0.0552/0.0722/0.0845 to 0.0175/0.0221/0.0311 world units. Shared-edge agreement, rather than zero error against the nonlinear field at an interior point, is what closes the row cracks. A final source audit also moved Uriel's 176 missed orange fringe pixels and Michael's two missed edge pixels onto the flame's weapon ownership.

Further close-up captures distinguished three remaining failure modes. A dark rectangle can be copied hair texture, an actual hole exposing the background, or an original dark outline that stayed on the cape after its limb moved. For Nuwa 129, the original automatic right-arm donor selected offset `(-51,-8)` in source-top coordinates and copied dark hair for 287 of 360 restored pixels. The measured `(0,+26)` drapery patch contains none of those dark donor pixels. Her original arm polygon also included the green right breastplate; the corrected concave contour leaves that plate on the torso. Her grey veil's upper edge now follows the forearm, fading into the existing cloth/body weights, and the crossed legs have a separate inner-dress continuation behind their original footprint.

Automatic garment restoration now requires a substantial opaque source surface, compares nearby surface colors when choosing a coherent 2D donor, and searches vertically as well as horizontally. Backdrop searches stop at source transparency instead of bridging air to a distant ribbon or bowstring. Measured cloth continuations may cross a body/cape ownership boundary without being replaced by a repeated boundary row. Dangun 130's sword-hidden skirt uses the original fabric above it; Bari 133's front skirt takes precedence over the separate rear red ribbon. A single-pass exterior arm-rim correction claims only cloth-owned pixels within three source pixels of an original arm and near source transparency. It cannot recursively grow into the body, face or held weapon.

Thor 111's knee centers were remeasured against the 229×293 original at top coordinates `(111,230)` and `(181,228)`, corresponding to bottom-origin `(111,63)` and `(181,65)`. The earlier points lay on the lower armor rim and produced upper/lower leg-length ratios of 3.32 and 3.34. The corrected knee measurements reduce those ratios to 1.77 and 1.49 before the subsequent right-ankle centering adjustment. The evidence and measured-coordinate overlay are in `Artifacts/TowerLimbReview/FeetAudit/111-knee-correction.json` and `111-knees-before-after.png`.

`TowerMythModel.Vertices.cs` now merges only exactly equal position/part/UV/mode/color/underpaint vertices when constructing an immutable template. The original triangle sequence, UV seams and four-pixel deformation grid remain intact. Posed buffers, matrices, meshes and material property blocks remain per-instance; a live two-instance check measured zero change in the untouched actor. `Artifacts/TowerLimbReview/mesh-performance.json` records 18,144 vertices per Thor instance and 8.243 ms per frame for four instances over 120 editor frames, compared with 25.083 ms before compaction. This is an editor animation benchmark, not a claim about whole-game frame rate. This benchmark preceded the final outline corrections; their later appearance and standalone results are recorded separately below.

The subsequent `ReleaseGallery` review used copied-versus-original mesh captures and binary vertex/UV dumps to distinguish source errors from restoration errors. Freyja 113's lower restoration mapped target x=167..174, top y=248 onto donor x=177..178, top y=249; Bari 133 mapped an entire rectangle onto one narrow red-ribbon row. The offset search previously gave missing donor pixels zero cost, then repaired each miss with a reach-wide nearest-pixel search. Missing coverage now incurs a penalty, garment donors can cross body/cape ownership boundaries, and local fallback is limited to three source pixels. Freyja's affected hem uses a broad red-cape patch; Bari does not restore outside her measured front-dress silhouette. Backdrop rays also stop below alpha 180, so faint source fringes do not turn transparent spaces inside ribbon loops into solid restoration panels.

The bounded exterior-rim pass now also covers head ornaments, legs and primary weapons. Every pass reads a frozen ownership snapshot, changes only cloth ownership 13, and requires both a neighboring measured part within three source pixels and source transparency within four. Existing body, face, limb and prop ownership is never overwritten. This targets the remaining detached horn, boot and weapon outlines without deleting source pixels. Captured-pose review was still outstanding at this point and was performed in the later galleries described below; the earlier motion numbers alone did not certify appearance.

The `SurfaceCandidate` review exposed real missing cape areas after removal of the repeated-row restoration. Zeus 105's narrow purple cape, Thor 111's red cape and Hwanung 132's blue/white rear robe now use separately registered original alternate-pose pixels. Their source rows begin at atlas-top 757, 516 and 748 respectively. Source and destination polygons, source alpha, and the original covered arm ownership all constrain each patch; the hand's surrounding air is not filled. Joint overlap pixels continue to follow the arm, while only guessed stationary costume patches are replaced. The original image files and runtime animation source frame remain unchanged.

Bari, Jacheongbi, Solomon and Moon Rabbit use measured front-garment curves rather than a rectangular pelvis region where that region cut across a separate flowing ribbon or cape. Their narrow cloth-to-body blend follows the same curve. Restoration behind lower held objects is clipped to the front garment, preserving the original open space inside rear loops. Dangun's large lower white panel remains a waist garment: comparison of all six original poses showed that it stays at the waist when his arm rises, so attaching the entire panel to the forearm would be incorrect. The captured-pose review for this stage appears in the subsequent SurfaceEdges, CostumeFinal and VerifiedRelease results below.

The remaining stepped cloth seams were reproduced directly from actual posed mesh exports. A row can end between the four-pixel grid columns; its explicit endpoint then disagrees with the neighboring row's interpolation at the same source position. `Tests/Check-TowerClothEdges.py` measures those T-junctions from exported triangles without reproducing the skinning code. `SurfaceMeasured/cloth-edges-before.json` records maximum gaps of 0.01281, 0.01758 and 0.01073 world units for 113, 134 and 138. Only cloth cells that touch the nonlinear body-blend band now use individual source-pixel columns; constant-weight regions keep the compact four-pixel grid. The source UVs, poses and bone weights are unchanged by this subdivision.

`SurfaceEdges/cloth-edges-after.json` passes the same 0.001-world-unit limit: the three maximum differences fall to approximately 0.00000024 world units. Vertex counts for 113/134/138 change from 13,134/13,347/15,967 to 14,200/14,310/17,844. Visual review separately confirmed that Jacheongbi's horizontal white restoration bar disappeared and Solomon's legitimate small blue rear fold reconnected; the blue piece was not deleted. The check does not certify every nonlinear limb or veil binding.

Bari's old clamped-row bell restoration is replaced by a bounded original cast-dress patch (atlas-top row 748), restricted to the bell/inner-arm footprint. The outer sleeve has hair or transparent air behind it in the original alternate poses, so that space remains open. The last review also distinguished stretching from a crack: Freyja's narrow right red hem and Solomon's narrow rear blue fold broadened when their inner edge followed the body and their outer edge followed the large cape swing. Those complete lower folds now follow the adjacent skirt with a gradual transition above them, preserving their painted width.

All thirty poses in `Artifacts/TowerLimbReview/CostumeFinal/{113,133,138}.png` were reviewed after the final binding and donor changes. Freyja and Solomon retain their narrow rear hems without the broad striped triangles, and Bari's two rectangular white restoration remnants are gone; only the actual dress behind the bell is continued. Earlier `SurfaceMeasured` captures also confirmed Zeus and Thor's cape continuations, Hwanung's closed rear-robe cutout and Loki's complete horn silhouette. This accepts the focused appearance changes; final whole-catalog and standalone validation is recorded separately.

The subsequent fifty-character `VerifiedRelease` review found additional pre-existing defects, so that gallery is not blanket visual acceptance. Gabriel, Raphael, Metatron and Ra had angular cuts in costume behind their moving left arms. Comparison with their six original poses confirmed actual hidden blue, green, purple and red cloth, while the outer wing/cape air gaps remained legitimate. Small source- and destination-bounded patches now use the original attack row 468 for 101/102/104 and cast row 790 for 120, only inside arm ownership 3/4. Gabriel's narrow right rear skirt is also bound to the neighboring skirt below its transition region to preserve its width. `WeaponAudit/cape-donor-draft-101.json` and `arm-donors-final-102-104-120.json` record the measurements. The subsequently rebuilt `VerifiedRelease/{101,102,104,120}-labeled.png` forty-pose set confirms that these arm-hidden costume cuts are closed while the original outer wing/cape spaces stay open. Gabriel's rear skirt also retains its original narrow width. Metatron's separate lower right garment required the same width-preserving binding below source-top 190. The refreshed ten-pose `104-labeled.png` confirms the lower striped expansion is removed while the higher right wing remains intact.

The same review found Isis 122's heel remaining on the staff and Fionn 141's original boots remaining outside the generic leg corridor. Separate measured leg outlines and corrected Fionn knee/ankle/sole anchors address the source ownership. Isis's measured right-sandal polygon takes precedence over the overlapping staff so the complete shoe follows foot ownership 12. A sole-center probe alone cannot detect a second shoe left on another part. The follow-up Isis gallery exposed a separate binding error: her old hem of bottom-origin 17 pinned the visible shin and the generated hidden shin to the pelvis while the rigid sandal stepped forward. The source-ownership correction alone was therefore insufficient; the later hem/hidden-shin correction and its movement review are recorded next.

Isis now blends the two leg roots near their measured knees rather than the floor-length robe hem. Her already painted left calf does not receive a redundant hidden-shin surface; the occluded right shin retains its continuation. The change is limited to 122 and leaves rigid shoe ownership unchanged. Original calf pixels at source-top `(98,265)` and `(100,266)` provide independent movement probes: they must move with the lower leg instead of remaining at the pelvis transform. The rebuilt `VerifiedRelease/122-labeled.png` and `122-final-feet-review.png` confirm that both walking poses retain two shoes and a connected right shin, without the previous stationary extra calf/shoe. Runtime regression is recorded separately.

The refreshed `VerifiedRelease/{100..124}-labeled.png` set, written at 19:02 KST on 2026-10-20 after the cadence and Isis corrections, received a final individual review of all 250 panels. This review covered both walking phases, anticipation, contact, follow-through, recovery, casting and mirrored contact against the rest/ownership panels. No new severe limb separation, duplicate boot, detached held-object section or broad costume-restoration panel was found in these twenty-five characters. The bounded cape repairs for 101/102/104/120 and Metatron's narrow lower garment remained intact. This is acceptance of the captured appearance for this subset, not a substitute for continuous-motion or standalone tests. Small source-edge flecks and angular deformation of the single painted viewpoint remain visible limitations. Fionn 141's separately identified sword-hidden knee/inner-garment gap is outside this accepted subset and is tracked by its focused follow-up.

Fionn's final follow-up uses three bounded patches from the original cast row at atlas-top 765: the dark green inner skirt, the left inner knee and the right inner shin. The patch targets intersect only existing sword ownership 14; the leg patches additionally intersect their measured leg outlines. `WeaponAudit/141-sword-backfill-draft.json` records the original source and destination registration, and `141-sword-backfill-actual-owners.png` shows the source-pixel comparison before runtime posing. The diagnostic restored 570/111/413 pixels respectively and wrote no pixels into the protected space between the lower legs (top-origin x=110..127, y=250..274). Managed C# compilation passed.

The refreshed `VerifiedRelease/141-labeled.png` at 19:12 KST received a separate ten-panel review. The inner skirt and both knee/shin surfaces continue behind the raised sword in contact and mirrored contact; the natural space between the calves remains open. Both walking poses keep two connected shoes, and no new severe silhouette defect was found. The focused `Artifacts/TowerLimbReview/fionn-final-focused.json` invocation recorded 684 motion samples with no failures or errors, including rigid shoes, maintained contacts and action recovery. This was a focused character invocation rather than a completed whole-suite report. Runtime source is frozen; the final whole-catalog executable result is recorded separately.

## Whole-catalog executable validation — 2026-10-20

`Artifacts/TowerLimbReview/ReleaseAnimation/animation-report.json` completed successfully at `2026-10-20T10:29:50Z`, with no failures or errors: all fifty companions / 34,200 motion samples, fifty companions / 3,600 stopping samples, and sixty-six rigs / 594 transition checks passed. This final rebuilt-executable run includes the measured limbs, cape/garment repairs, Isis shin binding, Fionn sword backfill, cadence/stop corrections, bounded arm-joint turning and Rat Champion's final dagger outline. It supersedes the earlier candidate's jump/dash and attack-entry failures; those earlier results remain diagnostic history.

`Artifacts/TowerLimbReview/ReleaseMonsters/monster-rigs-report.json` also reports success with no errors: forty-nine theme creatures plus five legacy creatures, 17,934 locomotion samples and 3,618 attack samples. `Artifacts/TowerDemo/companions-report.json` passed all 266 checks at `2026-10-20T10:16:41Z`, including fifty basic attacks and two hundred learned skills. `Artifacts/TowerDemo/combat-report.json` passed all thirty-nine behavior flags at `2026-10-20T10:17:37Z`, including melee and ranged damage against airborne targets, reachable-platform climbing, formation behavior, projectile contacts and companion immunity.

An editor-only cleanup uses `DestroyImmediate` for the temporary pixel-read texture outside Play mode and retains `Destroy` during play. The conditional is excluded from Player builds; the editor console was subsequently checked with zero errors.

Rat Champion 145's lower dagger contour now includes 123 additional source-alpha pixels without losing any previously included weapon pixels. Thirty-one of the missed pixels had remained after the general exterior-rim pass. The local contour change preserves the adjacent costume and arm, as recorded in `Artifacts/TowerLimbReview/WeaponAudit/145-weapon-lower-edge-check.json`. Five independently measured bright blade-edge probes now follow weapon ownership 14; `landmarks-final.json` passes all 38 independent probes, 99 visible hands, 100 soles and three excluded atlas fragments. The focused 145 invocation recorded 684 motion samples with no failures or errors, and direct review of the updated `VerifiedRelease/145.png` contact/cast panels confirmed that the floating white edge disappeared while the blade and handle stayed connected. The final whole-catalog executable result above includes this correction.
