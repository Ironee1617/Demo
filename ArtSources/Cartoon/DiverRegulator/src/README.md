# R58 diver regulator patch sources

Python (numpy, scipy, Pillow). Builds `../<mesh>.dreg` from Unity mesh exports of the three diver meshes.

- Inputs: `Temp/ClaudeQA/diverreg/<mesh>.raw/.json/.uv2` (raw mesh export: positions, normals, uv, bone index/weight,
  triangles; uv2 separately), the atlases in `Equipment/Resources/AquariumEquipment`, and the part labels here:
  `yel_lab.npy` (yellow crease segments per triangle), `seg_Diver_Drone.npy` (pink), `hose_info.json` (old hose
  anchors under the earpieces). The scripts read these from `/tmp` and `/mnt/user-data/uploads/D:--Demo/...`;
  change the paths in `dload.py` / `regulator.py` to rerun elsewhere.
- `regulator.py build(name)`: removes the old throat regulator and hose, caps the holes (`caps.py`), adds the new
  regulator, hose, yellow inflator fitting and cable end. `write_patch.py` writes DREG v2 patches.
- Unity side: `Equipment/Editor/DiverRegulator.cs` (menu Aquarium/Characters/Regulator in the divers' mouths).
