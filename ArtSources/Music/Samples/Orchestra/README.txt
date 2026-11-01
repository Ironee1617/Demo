VSCO 2 Community Edition: curated orchestral sources

Original library: VSCO 2 CE (VS Chamber Orchestra: Community Edition), Versilian Studios
Recorded by Sam Gossner & Simon Dalzell; sample cutting by Elan Hickler / Soundemote
Repository: https://github.com/sgossner/VSCO-2-CE
Pinned commit: 440300901dfe9275fd84e0b7763af1f8443ae62e
License: CC0 1.0 Universal. Full text is in LICENSE-CC0.txt (copied from the repository).
Accessed: 2026-10-27.

Selections (see manifest.json "selection"): Keys/Upright Nr1 (piano, pp/mf/f, 2 round robins),
Woodwinds Flute (susNV, susvib, stac), Oboe (Vib, Stacc), Clarinet (susLong, stac), Strings Solo Violin
(Arco Vib, spic), Cello Section (susvib), Violin Section (susVib), Harp, Percussion Timpani, Glock,
tubular bells (TB_hit), bass drum (BDrumNewhit), claves, congas (Conga/Quinto/Tumba hits), gong and log drums.

Processing (prepare_samples.py): leading silence trimmed with 2 ms kept, sustains shortened (piano 6 s,
sustains 4.5 s, others per selection) with an 80 ms fade, short samples given a 15 ms tail fade, stereo and
sample rate kept, FLAC output. No instrument waveform is synthesized. Source and output SHA256 values, MIDI
roots (VSCO names middle C as C3, so C3 = 60), velocity layers (pp/mf/f or v1..vN), round robins and
measured intonation are in manifest.json.

To use: load manifest.json, choose the instrument and articulation, the nearest root_midi, the nearest
velocity layer, rotate round_robin, and resample to the target pitch (Tools/TowerMusic/sampled.py).
