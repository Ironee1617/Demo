# GodPixels

Bakes the 50 companion gods as chibi pixel sheets in the style of chuchuking's Zeus
(`Assets/RogueLike/Character/Zeus`). Zeus keeps that art; Odin and Sun Wukong are posed from
chuchuking's Spine characters (`Assets/RogueLike/Character/Spine`, fists and weapons from
`Assets/RogueLike/Character/Weapons`); the other 47 are assembled from ASCII parts measured on
the same 61x75 frame. The player hero comes from chuchuking's Spine `Player` the same way.
See `Docs/Roguelike-God-Pixels.md`.

```bash
python paint_gods.py                                   # all sheets + Gods.json
python paint_gods.py --only athena --no-write --preview <dir>
python paint_hero.py                                   # Resources/Tower/Hero/Hero.png + Hero.json
```

Re-run both after chuchuking updates a Spine export. `spinekit.py` reads only what his flat
skeletons use (region attachments, bone rotate/translate/scale, attachment and draw-order keys)
and stops on anything else, so a richer export fails instead of baking wrong.

| File | Contents |
| --- | --- |
| `pixkit.py` | canvas, legends, colour ramps, rim shading, per-part outline rings |
| `body.py` | face, eye shapes and brows, mouths, torsos and builds, worn parts, boots, Zeus's measured cape frames |
| `hairstyles.py`, `hair.py` | hand-drawn hair in Zeus's clump language |
| `heads.py` | beards, crowns, helmets, hoods, ears, horns, face marks |
| `extras.py` | wings, tails, quiver, sash, wheels, serpent body, fists |
| `weapons.py` | weapons and off-hand items, drawn pointing up with a grip pixel |
| `gods.py` | the roster: palette and parts per god |
| `paint_gods.py` | frames, sheet packing, `Gods.json`, card portraits, previews |
| `spinekit.py` | reads chuchuking's Spine 4.2 `.skel.bytes` and atlases, poses them into pixel cells |
| `paint_hero.py` | the player hero's sheet: body poses, hands, sword, crossbow, orb, bolt, card |

Every hairstyle is cut from Zeus's own hair silhouette (crown and sides within x 17..40; long hair
falls lower) with its outline drawn by hand;
`hair.check()` rejects rows that are not 28 wide or that cover the eyes (columns 13..22, rows 33..38).
Render a preview after editing a hairstyle or face.

Bodies vary in width only, never in height: `body.fit()` repeats (broad, stout) or drops (slim)
columns 29 and 32 of a torso or worn part, so parts left of the chest move out and parts right of
it move the other way. Worn parts in `body.WEAR` are drawn for a normal build and fitted the same way.
