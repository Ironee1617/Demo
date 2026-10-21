using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerMythModel
    {
        struct PaintedVertex : IEquatable<PaintedVertex>
        {
            public Vector3 position;
            public Vector2 uv, mode;
            public Color32 color;
            public int part;
            public bool underpaint;

            public bool Equals(PaintedVertex other)
            {
                // Exact comparisons preserve every UV seam, surface and weight.
                return position.Equals(other.position) && uv.Equals(other.uv) &&
                    mode.Equals(other.mode) && color.Equals(other.color) &&
                    part == other.part && underpaint == other.underpaint;
            }
            public override bool Equals(object other)
            { return other is PaintedVertex && Equals((PaintedVertex)other); }
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = position.GetHashCode();
                    hash = hash * 31 + uv.GetHashCode();
                    hash = hash * 31 + mode.GetHashCode();
                    hash = hash * 31 + color.GetHashCode();
                    hash = hash * 31 + part;
                    return hash * 31 + (underpaint ? 1 : 0);
                }
            }
        }

        void CompactPaintedVertices()
        {
            // Neighboring grid cells share corners. Skin each identical corner
            // once, retaining triangle order and the full deformation grid.
            int count = buildVertices.Count, unique = 0;
            var lookup = new Dictionary<PaintedVertex, int>(count);
            var remap = new int[count];
            for (int read = 0; read < count; read++)
            {
                var key = new PaintedVertex {
                    position = buildVertices[read], uv = buildUvs[read],
                    mode = buildModes[read], color = buildColors[read],
                    part = buildParts[read], underpaint = buildUnderpaint[read]
                };
                int index;
                if (!lookup.TryGetValue(key, out index))
                {
                    index = unique++;
                    lookup.Add(key, index);
                    buildVertices[index] = key.position;
                    buildUvs[index] = key.uv;
                    buildModes[index] = key.mode;
                    buildColors[index] = key.color;
                    buildParts[index] = key.part;
                    buildUnderpaint[index] = key.underpaint;
                }
                remap[read] = index;
            }
            for (int i = 0; i < triangles.Count; i++) triangles[i] = remap[triangles[i]];
            buildVertices.RemoveRange(unique, count - unique);
            buildUvs.RemoveRange(unique, count - unique);
            buildModes.RemoveRange(unique, count - unique);
            buildColors.RemoveRange(unique, count - unique);
            buildParts.RemoveRange(unique, count - unique);
            buildUnderpaint.RemoveRange(unique, count - unique);
        }
    }
}
