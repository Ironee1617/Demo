using StoryPainter;

// Usage: dotnet run -c Release --project Tools/TowerArt/StoryPainter -- [--out <dir>] [--preview <dir>] [--zoom x0 y0 size]
var argsList = args.ToList();
string Arg(string name) { int k = argsList.IndexOf(name); return k >= 0 && k + 1 < argsList.Count ? argsList[k + 1] : null; }
var dir = new DirectoryInfo(AppContext.BaseDirectory);
while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets"))) dir = dir.Parent;
string projectRoot = dir?.FullName ?? Directory.GetCurrentDirectory();
string output = Arg("--out") ?? Path.Combine(projectRoot, "Assets", "Demos", "Roguelike", "Resources", "Tower", "Story");
string preview = Arg("--preview");

var paintings = new List<(string name, Img img)> { ("nihilos", Nihilos.Paint()) };
paintings.AddRange(VillageProps.Paint());
foreach (var (name, img) in paintings)
{
    string path = Path.Combine(output, name + ".png");
    Png.Write(path, img.W, img.H, img.Rgba());
    Console.WriteLine(name + ": " + Path.GetFullPath(path));
    // A 2x nearest-neighbour enlargement over the abyss's near-black red for inspection.
    if (preview != null) Png.Write(Path.Combine(preview, name + "-preview.png"), img.W * 2, img.H * 2, img.Rgba(Col.Hex("1a0b10"), 2));
}
// --zoom x0 y0 size: a 4x close-up of one square region of the boss (bottom-left origin) for inspecting shading.
int z = argsList.IndexOf("--zoom");
if (preview != null && z >= 0 && z + 3 < argsList.Count)
{
    var boss = paintings[0].img;
    int x0 = int.Parse(argsList[z + 1]), y0 = int.Parse(argsList[z + 2]), size = int.Parse(argsList[z + 3]);
    var crop = new Img(size, size);
    for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) crop.P[y * size + x] = boss.Get(x0 + x, y0 + y);
    Png.Write(Path.Combine(preview, "nihilos-zoom.png"), size * 4, size * 4, crop.Rgba(Col.Hex("1a0b10"), 4));
}
