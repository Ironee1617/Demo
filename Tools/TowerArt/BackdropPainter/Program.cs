using System.Diagnostics;
using System.Text;
using BackdropPainter;

// Usage: dotnet run -c Release -- [--theme lava] [--out <Assets backdrop dir>] [--preview <dir>] [--terrain]
var argsList = args.ToList();
string Arg(string name) { int k = argsList.IndexOf(name); return k >= 0 && k + 1 < argsList.Count ? argsList[k + 1] : null; }
string projectRoot = FindProjectRoot();
string outDir = Arg("--out") ?? Path.Combine(projectRoot, "Assets", "Demos", "Roguelike", "Resources", "Tower", "Themes", "Backdrops");
string previewDir = Arg("--preview");
string only = Arg("--theme");
bool terrainOnly = argsList.Contains("--terrain-only");
// --fast renders lit themes at half resolution for previews only; it never writes assets.
bool fast = argsList.Contains("--fast");
if (fast) { BackdropPainter.Render.Bake.Scale = .5f; BackdropPainter.Render.Bake.Samples = 2; }

// TowerThemeId order: the terrain atlas column and the creature-board mapping both index by it.
var themes = new ThemePainter[] { new OlympusScene(), new AsgardScene(), new DangunScene(), new EdenScene(), new VedaScene(), new PapyrusScene(), new ChineseScene(), new HellScene(), new AbyssScene(), new VillageScene() };
// Lit terrain kits (one per theme that has been converted to the physically lit style).
var kits = new BackdropPainter.Render.TerrainKit[] { new OlympusTerrain(), new AsgardTerrain(), new DangunTerrain(), new EdenTerrain(), new VedaTerrain(), new PapyrusTerrain(), new ChineseTerrain(), new HellTerrain(), new AbyssTerrain(), new VillageTerrain() };
var manifest = new List<string>();
string manifestPath = Path.Combine(outDir, "Backdrops.json");
var previous = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : null;

// --probe <theme> <x> <v>: diagnose one point of a theme's lit surface band, then stop.
if (argsList.Contains("--probe"))
{
    var kit = kits.First(k => k.Id == Arg("--probe"));
    int at = argsList.IndexOf("--probe");
    float px = float.Parse(argsList[at + 2], System.Globalization.CultureInfo.InvariantCulture), pv = float.Parse(argsList[at + 3], System.Globalization.CultureInfo.InvariantCulture);
    var probeScene = new BackdropPainter.Render.Scene(12); kit.Top(probeScene, new BackdropPainter.Render.Mats(probeScene));
    Console.WriteLine(new BackdropPainter.Render.Renderer(probeScene, kit.Light(), new BackdropPainter.Render.Camera(10, 20, 60)).Probe(px, pv));
    return;
}

if (!terrainOnly)
{
    foreach (var theme in themes)
    {
        if (only != null && theme.Id != only) { manifest.AddRange(ExistingEntries(previous, theme.Id)); continue; }
        var sw = Stopwatch.StartNew();
        var layers = theme.Paint().ToList();
        if (!fast) for (int k = 0; k < layers.Count; k++) manifest.AddRange(Export(theme, layers[k], k, outDir));
        Console.WriteLine($"{theme.Id}: {layers.Count} planes in {sw.ElapsedMilliseconds} ms");
        if (previewDir != null) Preview.Render(theme, layers, previewDir);
    }
    if (!fast) File.WriteAllText(manifestPath, "{\n  \"layers\": [\n" + string.Join(",\n", manifest) + "\n  ]\n}\n", new UTF8Encoding(false));
}
if (argsList.Contains("--terrain") || argsList.Contains("--kits") || terrainOnly)
{
    string kitDir = Path.Combine(outDir, "..", "Terrain");
    foreach (var kit in kits) if (only == null || kit.Id == only) BackdropPainter.Render.TerrainBake.Bake(kit, kitDir, previewDir);
}
if ((argsList.Contains("--terrain") || terrainOnly) && !argsList.Contains("--kits"))
{
    string terrainPath = Path.Combine(outDir, "..", "Terrain.png");
    TerrainAtlas.Paint(terrainPath, previewDir);
    Console.WriteLine("terrain atlas: " + Path.GetFullPath(terrainPath));
}

static IEnumerable<string> ExistingEntries(string json, string theme)
{
    if (json == null) yield break;
    foreach (var line in json.Split('\n'))
        if (line.Contains("\"theme\": \"" + theme + "\"")) yield return line.TrimEnd(',', '\r');
}

static IEnumerable<string> Export(ThemePainter theme, PaintedLayer layer, int index, string outDir)
{
    var c = layer.Canvas; var s = layer.Spec;
    c.Bleed();
    // Split the strip into bands of occupied rows; long transparent gaps are not stored.
    var runs = new List<(int start, int end)>();
    for (int j = 0; j < c.H; j++)
    {
        if (c.RowEmpty(j)) continue;
        if (runs.Count > 0 && j - runs[^1].end <= 96) runs[^1] = (runs[^1].start, j);
        else runs.Add((j, j));
    }
    // Keep heights divisible by four for block compression, growing into transparent rows.
    var bands = runs.Select(r =>
    {
        int rows = (r.end - r.start + 4) / 4 * 4, start = r.start;
        if (start + rows > c.H) start = c.H - rows;
        return (start, end: start + rows - 1);
    }).ToList();
    for (int b = 0; b < bands.Count; b++)
    {
        var (start, end) = bands[b];
        int rows = end - start + 1;
        string file = $"{theme.Id}-{index}-{s.Name}" + (bands.Count > 1 ? "-" + b : "");
        Png.Write(Path.Combine(outDir, file + ".png"), c.W, rows, c.ToBytes(start, rows));
        float bottom = c.Bottom + start / c.Ppu;
        string under = b == 0 && s.Under.HasValue ? s.Under.Value.ToHex() : "";
        string over = b == bands.Count - 1 && s.Over.HasValue ? s.Over.Value.ToHex() : "";
        yield return FormattableString.Invariant(
            $"    {{\"theme\": \"{theme.Id}\", \"file\": \"{file}\", \"plane\": \"{s.Name}\", \"factor\": {s.Factor}, \"ppu\": {s.Ppu}, \"period\": {s.Period}, \"bottom\": {bottom:0.#####}, \"width\": {c.W}, \"height\": {rows}, \"order\": {s.Order}, \"under\": \"{under}\", \"over\": \"{over}\"}}");
    }
}

static string FindProjectRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets"))) dir = dir.Parent;
    return dir?.FullName ?? Directory.GetCurrentDirectory();
}
