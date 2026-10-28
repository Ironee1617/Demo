VSCO 2 Community Edition: curated brass sources

Original library: VSCO 2 CE (VS Chamber Orchestra: Community Edition), Versilian Studios
Recorded by Sam Gossner & Simon Dalzell; sample cutting by Elan Hickler / Soundemote
Repository: https://github.com/sgossner/VSCO-2-CE
Pinned commit: 440300901dfe9275fd84e0b7763af1f8443ae62e
License: CC0 1.0 Universal. Full text is in LICENSE-CC0.txt (copied from the repository).
Accessed: 2026-10-27.

Selections: Brass/Trumpet (sus, stac), Brass/F Horn (sus, stac), Brass/Tenor Trombone (sus, stac):
every velocity layer and round robin of those folders. Tuba, muted and vibrato folders were not used.

Processing (prepare_samples.py): common leading silence trimmed with 2 ms kept, sustains shortened to
4.5 s with an 80 ms fade, staccatos given a 15 ms tail fade, one fixed gain per instrument and
articulation, stereo and 44.1 kHz kept, FLAC output. No instrument waveform is synthesized.
Source and output SHA256 values, note names, MIDI roots (VSCO names middle C as C3, so C3 = 60),
velocity layers, round robins and measured intonation are in manifest.json.

To use: load manifest.json, choose the instrument by register, the nearest root_midi, the nearest
velocity layer, rotate round_robin, and resample to the target pitch. tuning_cents is the measured
offset from the named note where the measurement was reliable, else 0.
