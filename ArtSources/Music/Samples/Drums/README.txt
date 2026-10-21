Big Rusty Drums: curated acoustic rock drum sources

Original library: Big Rusty Drums, Karoryfer Samples / Karoryfer Lecolds
Official product: https://shop.karoryfer.com/pages/free-big-rusty-drums
Official license statement: https://shop.karoryfer.com/pages/free-samples
Repository: https://github.com/sfzinstruments/karoryfer.big-rusty-drums
Pinned commit: f07ce00df34a46b6b08375be56fe116cf15782bc
License: CC0 1.0 Universal. Full text is in LICENSE-CC0.txt.
Accessed: 2026-10-20.

The official Karoryfer free-samples page identifies all its free libraries as CC0,
including older downloads formerly carrying a more restrictive license. The
pinned repository independently contains the CC0 license. CC0 allows copying,
adaptation, commercial use and redistribution. Attribution is not required;
the author and source are retained here as provenance and courtesy credit.

Selections: real sticks on a 24-inch Szpaderski kick, 14-inch Szpaderski snare,
14/15/18-inch toms, 14-inch Sabian HHX Evolution hi-hat, 22-inch Sabian Big & Ugly
King ride and 17-inch Sabian AAX V-Crash. All recordings come from the same kit.

Original FLAC files preserve upstream bytes, including their natural mic
balance and timing. mixed/ contains curated stereo blends of the close and
overhead mics, with a small amount of bottom microphone on the snare. Only common
leading silence, the last 15ms of each tail and one fixed gain per articulation
were changed. The render includes real transient, room and cymbal decay detail.
No instrument waveform is synthesized. Source and output SHA256 are in manifest.json.

To use: load manifest.json samples, resolve file from this folder, choose the
nearest velocity layer and rotate among round_robin hits. The velocity field is
in MIDI 1-127 units only as a performance-strength coordinate; the sound files
are real recorded acoustic drums. Do not normalize each hit separately, as that
would destroy the recorded performance dynamics. Apply overall gains per kind.
Suggested mapping: tom -> tom_mid; ohat -> open_hat; accented backbeats may select
snare_rimshot. At fast tempos, choke open_hat when hat hits, and rotate kick and
snare round robins. The mixtures contain overhead room sound already, so use
additional reverb sparingly.

prepare_samples.py reproduces the download and mixes from the pinned repository.
source-tree.json records the upstream file inventory used in the selection.
