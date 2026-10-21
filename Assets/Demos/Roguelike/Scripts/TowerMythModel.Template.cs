using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerMythModel
    {
        // Source ownership, repaired surfaces and skin bindings are unchanged by
        // posing. Build them once per anatomy/source slice, while every actor keeps
        // its own posed vertex buffer, Mesh, matrices and material property block.
        sealed class ModelTemplate
        {
            public readonly byte[] ownership;
            public readonly Vector3[] rest;
            public readonly int[] parts, triangles;
            public readonly bool[] underpaint;
            public readonly SkinBinding[] bindings;
            public readonly Vector2[] uvs, modes;
            public readonly Color32[] colors;
            public readonly int partCount;

            public ModelTemplate(TowerMythModel model)
            {
                ownership = model.ownership;
                rest = model.rest;
                parts = model.parts;
                underpaint = model.underpaint;
                bindings = model.bindings;
                uvs = model.buildUvs.ToArray();
                modes = model.buildModes.ToArray();
                colors = model.buildColors.ToArray();
                triangles = model.triangles.ToArray();
                partCount = model.PartCountPresent;
            }
        }

        struct TemplateKey : IEquatable<TemplateKey>
        {
            readonly TowerMythRig.Profile anatomy;
            readonly Texture2D texture;
            readonly int x, y, width, height;

            public TemplateKey(TowerMythModel model)
            {
                anatomy = model.profile;
                texture = model.texture;
                x = model.sourceX; y = model.sourceY;
                width = model.width; height = model.height;
            }

            public bool Equals(TemplateKey other)
            {
                return ReferenceEquals(anatomy, other.anatomy) &&
                    ReferenceEquals(texture, other.texture) &&
                    x == other.x && y == other.y &&
                    width == other.width && height == other.height;
            }

            public override bool Equals(object value)
            { return value is TemplateKey && Equals((TemplateKey)value); }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = RuntimeHelpers.GetHashCode(anatomy);
                    hash = hash * 31 + RuntimeHelpers.GetHashCode(texture);
                    hash = hash * 31 + x; hash = hash * 31 + y;
                    hash = hash * 31 + width; return hash * 31 + height;
                }
            }
        }

        static readonly Dictionary<TemplateKey, ModelTemplate> templates =
            new Dictionary<TemplateKey, ModelTemplate>();

        // The authored profiles are immutable after loading. A freshly loaded
        // profile gets a new key even when its art index is unchanged. Also clear
        // between play sessions when the Editor has domain reload disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTemplates() { templates.Clear(); }

        ModelTemplate FindTemplate()
        {
            ModelTemplate result;
            return templates.TryGetValue(new TemplateKey(this), out result) ? result : null;
        }

        ModelTemplate StoreTemplate()
        {
            var result = new ModelTemplate(this);
            templates[new TemplateKey(this)] = result;
            // List.Clear alone retains all construction buffers on the first
            // living instance, duplicating the immutable template's arrays.
            ReleaseBuildBuffer(buildVertices);
            ReleaseBuildBuffer(buildUvs);
            ReleaseBuildBuffer(buildModes);
            ReleaseBuildBuffer(buildColors);
            ReleaseBuildBuffer(buildParts);
            ReleaseBuildBuffer(buildUnderpaint);
            ReleaseBuildBuffer(triangles);
            ReleaseBuildBuffer(patches);
            return result;
        }

        static void ReleaseBuildBuffer<T>(List<T> buffer)
        { buffer.Clear(); buffer.Capacity = 0; }
    }
}
