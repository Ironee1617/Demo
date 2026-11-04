using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R47: the three playable divers. One diver per game; each has an active (R, usable from 3 minutes into
    // the hunt) and a passive. Ids go over the network (Swimmer.diverKind), so new kinds go at the end.
    [Serializable]
    public sealed class AquariumDiverKind
    {
        public int id;
        public string key, name, look, activeName, activeText, passiveName, passiveText;
        public float harpoonRange = 5f, harpoonSpeedScale = 1f;
        public float r, g, b;
        // R53: model suffix in Resources/AquariumEquipment ("" = the yellow diver, "_Tracker" blue, "_Drone" pink).
        public string model = "";
    }

    public static class AquariumDiverKinds
    {
        public static readonly AquariumDiverKind[] All =
        {
            new AquariumDiverKind { id = 0, key = "Keeper", name = "사육사 잠수부", look = "먹이 주머니, 접착제 통",
                activeName = "접착제 바르기", activeText = "닫힌 배수구 뚜껑 테두리에 접착제를 덕지덕지 발라, 외계인이 나사도 뚜껑도 못 쓰게 막습니다. (게임당 1회)",
                passiveName = "먹잘알", passiveText = "뿌린 먹이에 상어, 가오리까지 모든 어종이 몰려듭니다. 작살 사거리 8m.",
                harpoonRange = 8f, r = .98f, g = .80f, b = .25f },
            new AquariumDiverKind { id = 1, key = "Tracker", name = "추적 잠수부", look = "탐지 고글, 가벼운 장비",
                activeName = "생체 탐지", activeText = "5초 동안 모든 생명체와 알이 벽 너머까지 빨간 실루엣으로 보입니다. (쿨타임 1분)",
                passiveName = "스프린트", passiveText = "질주 속도 +10%, 스태미나 80. 작살 사거리 5m.",
                harpoonRange = 5f, r = .95f, g = .42f, b = .35f, model = "_Tracker" },
            new AquariumDiverKind { id = 2, key = "Drone", name = "드론 잠수부", look = "물고기 드론 조종기",
                activeName = "드론 전문가", activeText = "물고기 드론으로 시점을 옮겨 자유롭게 감시합니다. 다시 누르면 돌아오고, 드론은 그 근처를 배회합니다. 외계인은 드론을 먹거나 위장할 수 없습니다.",
                passiveName = "예리한 사격", passiveText = "작살 사거리 8m, 작살 속도 +20%.",
                harpoonRange = 8f, harpoonSpeedScale = 1.2f, r = .35f, g = .85f, b = .95f, model = "_Drone" },
        };
        public static int Count { get { return All.Length; } }
        public static int Clamp(int kind) { return kind < 0 || kind >= All.Length ? 0 : kind; }
        public static AquariumDiverKind Get(int kind) { return All[Clamp(kind)]; }

        // R53: each diver has its own body (blue tracker, pink drone pilot); the first-person hands and the
        // harpoon gun share the yellow meshes with a recoloured atlas.
        private static readonly Dictionary<string, UnityEngine.Object> loaded = new Dictionary<string, UnityEngine.Object>();
        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            UnityEngine.Object o;
            if (!loaded.TryGetValue(path, out o)) { o = Resources.Load<T>(path); loaded[path] = o; }
            return o as T;
        }
        public static GameObject DiverPrefab(int kind)
        {
            string model = Get(kind).model;
            var prefab = model.Length > 0 ? Load<GameObject>("AquariumEquipment/Diver" + model) : null;
            return prefab != null ? prefab : Load<GameObject>("AquariumEquipment/Diver");
        }
        public static Material HandsMaterial(int kind)
        {
            string model = Get(kind).model;
            var m = model.Length > 0 ? Load<Material>("AquariumEquipment/DiverHands" + model + "_Cartoon") : null;
            return m != null ? m : Load<Material>("AquariumEquipment/Diver_Cartoon");
        }
        public static Material GunMaterial(int kind)
        {
            string model = Get(kind).model;
            var m = model.Length > 0 ? Load<Material>("AquariumEquipment/HarpoonGun" + model + "_Cartoon") : null;
            return m != null ? m : Load<Material>("AquariumEquipment/HarpoonGun_Cartoon");
        }
        // Every renderer of a held gun (barrel and loaded spear) takes the diver's gun paint.
        public static void PaintGun(Component gun, int kind)
        {
            if (gun == null) return;
            var m = GunMaterial(kind); if (m == null) return;
            foreach (var r in gun.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = m;
        }
    }
}
