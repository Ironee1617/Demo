using System.Diagnostics;
using System.Numerics;
using UIPainter;

// Usage: dotnet run -c Release --project Tools/TowerArt/UIPainter -- [--out <dir>] [--preview <dir>] [--no-background]
var argsList = args.ToList();
string Arg(string name) { int k = argsList.IndexOf(name); return k >= 0 && k + 1 < argsList.Count ? argsList[k + 1] : null; }
string outDir = Arg("--out") ?? Path.Combine(FindProjectRoot(), "Assets", "Demos", "Roguelike", "Resources", "Tower", "UI");
string previewDir = Arg("--preview");
var sw = Stopwatch.StartNew();

var pieces = new List<(string name, Img img)>
{
    ("panel-fill", Kit.PanelFill()),
    ("panel-shadow", Kit.PanelShadow()),
    ("panel-frame-gold", Kit.PanelFrame(Ramp.Gold, Ramp.Sapphire)),
    ("panel-frame-silver", Kit.PanelFrame(Ramp.Silver, Ramp.Glass)),
    ("card-frame-gold", Kit.CardFrame(Ramp.Gold, Ramp.Sapphire)),
    ("card-frame-silver", Kit.CardFrame(Ramp.Silver, Ramp.Glass)),
    ("crest-gold", Kit.Crest(Ramp.Gold, Ramp.Sapphire)),
    ("crest-silver", Kit.Crest(Ramp.Silver, Ramp.Glass)),
    ("button-body", Kit.ButtonBody()),
    ("button-edge-gold", Kit.ButtonEdge(Ramp.Gold)),
    ("button-edge-silver", Kit.ButtonEdge(Ramp.Silver)),
    ("inset", Kit.Inset()),
    ("portrait-frame-gold", Kit.PortraitFrame(Ramp.Gold, Ramp.Sapphire)),
    ("portrait-frame-silver", Kit.PortraitFrame(Ramp.Silver, Ramp.Glass)),
    ("medallion", Kit.Medallion(Ramp.Gold, Ramp.Ruby)),
    ("meter-trough", Kit.MeterTrough()),
    ("meter-rim-gold", Kit.MeterRim(Ramp.Gold)),
    ("meter-rim-silver", Kit.MeterRim(Ramp.Silver)),
    ("meter-fill", Kit.MeterFill()),
    ("divider", Kit.Divider()),
    ("knot", Kit.Knot(Ramp.Gold, Ramp.Sapphire)),
    ("keycap", Kit.Keycap()),
    ("flourish", Kit.Flourish(Ramp.Gold, Ramp.Sapphire)),
    ("node-disc", Kit.NodeDisc()),
    ("node-rim", Kit.NodeRim(Ramp.Silver)),
    ("key-fill", Kit.KeyFill()),
    ("key-rim", Kit.KeyRim(Ramp.Silver)),
    ("icons", Icons.Atlas()),
};
foreach (var (name, img) in pieces) img.Bleed().Save(Path.Combine(outDir, name + ".png"));
Console.WriteLine($"{pieces.Count} kit textures in {sw.ElapsedMilliseconds} ms -> {Path.GetFullPath(outDir)}");

Img background = null;
if (!argsList.Contains("--no-background"))
{
    sw.Restart();
    background = MenuBackground.Paint();
    background.Save(Path.Combine(outDir, "menu-backdrop.png"));
    Console.WriteLine($"menu backdrop in {sw.ElapsedMilliseconds} ms");
}

if (previewDir != null)
{
    // A contact sheet of every piece on a mid panel colour, at texture size.
    int x = 16, y = 16, rowH = 0, width = 1600;
    var sheet = new Img(width, 1100);
    sheet.Over((_, _) => new Vector4(.07f, .085f, .14f, 1));
    foreach (var (name, img) in pieces)
    {
        if (x + img.W + 16 > width) { x = 16; y += rowH + 16; rowH = 0; }
        sheet.Blit(img, x, y); x += img.W + 16; rowH = Math.Max(rowH, img.H);
    }
    sheet.Save(Path.Combine(previewDir, "ui-kit-sheet.png"));
    Console.WriteLine("preview: " + Path.GetFullPath(Path.Combine(previewDir, "ui-kit-sheet.png")));
}

static string FindProjectRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets"))) dir = dir.Parent;
    return dir?.FullName ?? Directory.GetCurrentDirectory();
}
