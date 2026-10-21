using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerMythModel
    {
        void AddHiddenLegs()
        {
            // A flattened robe contains shoes but no pixels for the covered
            // ankles. Continue its visible boot texture behind the robe, on the
            // same shin/foot weights, so a stepping shoe remains attached.
            // Bare legs and Nezha's complete wheels must keep their source skin.
            if (profile.artIndex == 126 || profile.artIndex == 129) return;
            float hem = Point(0, profile.legHemY).y;
            if (hem - Mathf.Min(leftFoot.y, rightFoot.y) > .24f) return;
            AddHiddenShin(true);
            AddHiddenShin(false);
        }

        void AddHiddenShin(bool left)
        {
            // Isis already has a fully painted left calf. Only the right ankle
            // is occluded by her robe and needs a texture continuation.
            if (profile.artIndex == 122 && left) return;
            Vector2 foot = left ? leftFoot : rightFoot, knee = left ? leftKnee : rightKnee;
            int firstPart = left ? 7 : 10, shin = firstPart + 1;
            int footY = Mathf.RoundToInt(foot.y / unit + profile.groundY);
            int footX = Mathf.RoundToInt(foot.x / unit + profile.groundX);
            int kneeY = Mathf.RoundToInt(knee.y / unit + profile.groundY);
            int kneeX = Mathf.RoundToInt(knee.x / unit + profile.groundX);
            int radius = Mathf.Max(3, Mathf.RoundToInt(.045f / unit));
            int sampleX = -1, sampleY = -1, best = int.MaxValue;
            int desiredY = footY + Mathf.RoundToInt(.055f / unit);
            int desiredX = Mathf.RoundToInt(Mathf.Lerp(footX, kneeX, .2f));
            // Use only this leg's painted pixels, never gold staff tips or skirt.
            for (int y = footY + 2; y <= Mathf.Min(kneeY, footY + 18); y++)
                for (int x = desiredX - 12; x <= desiredX + 12; x++)
                {
                    if (x < 1 || x >= width - 1 || y < 1 || y >= height - 1) continue;
                    int owner = ownership[y * mapWidth + x];
                    if (owner < firstPart || owner > firstPart + 2 || Pixel(x, y).a < 235) continue;
                    int distance = (x-desiredX)*(x-desiredX) + (y-desiredY)*(y-desiredY);
                    if (distance >= best) continue;
                    sampleX = x; sampleY = y; best = distance;
                }
            if (sampleX < 0) return;
            int top = Mathf.Min(height - 1, kneeY + Mathf.CeilToInt(.08f / unit));
            int cuffY=Mathf.RoundToInt((left?leftAnkle.y:rightAnkle.y)/unit+profile.groundY-.035f/unit);
            for (int y = Mathf.Max(footY+2,cuffY); y <= top; y++)
            {
                float along = Mathf.InverseLerp(footY, kneeY, y);
                int center = Mathf.RoundToInt(Mathf.Lerp(footX, kneeX, along));
                int halfWidth = Mathf.Max(2, Mathf.RoundToInt(radius * Mathf.Lerp(.8f, 1.15f, along)));
                // Stretch a single contiguous ankle patch. Repeating rows creates
                // a conspicuous ladder pattern when the shin leaves the robe.
                int sourceRow = sampleY + Mathf.RoundToInt(Mathf.Lerp(-2, 2, along));
                for (int dx = -halfWidth; dx <= halfWidth; dx++)
                {
                    int x = center + dx;
                    if (x < 0 || x >= width) continue;
                    int sy = sourceRow;
                    int sx = sampleX + Mathf.Clamp(dx, -radius, radius);
                    if (!HiddenLegSample(sx, sy, firstPart)) { sx = sampleX; sy = sampleY; }
                    AddTexturedPixel(x, y, sx, sy, shin);
                }
            }
        }
        bool HiddenLegSample(int x, int y, int firstPart)
        {
            if (x < 0 || x >= width || y < 0 || y >= height || Pixel(x, y).a < 235) return false;
            int owner = ownership[y * mapWidth + x];
            return owner >= firstPart && owner <= firstPart + 2;
        }
    }
}
