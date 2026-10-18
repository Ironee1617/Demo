# Unity project safety

- Keep disposable Unity validation projects outside this project's `Temp`, `Library`, `obj`, and build output folders. Unity can recursively clean these directories when opening or closing the project.
- Never junction, symlink, or hard-link a disposable project's `Assets` or `Packages` to this working project's source. Use independent file copies. Do not share writable source trees between Unity processes.
- Before cleaning or moving a directory recursively, resolve its absolute path and inspect reparse points without following them. Stop if any link reaches a source directory.
- Preserve `Assets` together with `.meta` files. Recover accidentally missing tracked files from Git without discarding unrelated work.

On 2026-10-18, the previous validation setup at `Temp/SwimmingQAProject` was found to have linked both `Assets` and `Packages` to the real project. Opening Unity left all 797 tracked Assets files deleted and reset the missing package manifest to defaults. An isolated disposable Unity 6000.3.21f1 probe confirmed that Temp cleanup follows junctions and deletes their target contents. Sources and package settings were restored from commit `f75d59f`. Do not recreate that validation setup.
