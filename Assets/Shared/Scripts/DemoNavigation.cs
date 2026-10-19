using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrototypeLab
{
    // Temporary navigation so every empty demo scene is easy to open in Play mode.
    public sealed class DemoNavigation : MonoBehaviour
    {
        private const string Hub = "00_Hub";
        private const string Survivors = "10_Survivors";
        private const string Roguelike = "20_Roguelike";
        private const string HideAndSeek = "30_HideAndSeek";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (FindFirstObjectByType<DemoNavigation>() != null)
                return;

            var navigation = new GameObject("Prototype Navigation");
            DontDestroyOnLoad(navigation);
            navigation.AddComponent<DemoNavigation>();
        }

        private void OnGUI()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (sceneName == HideAndSeek || sceneName == Roguelike) return;
            var width = Mathf.Min(400f, Screen.width - 32f);
            var height = sceneName == Hub ? 225f : 145f;
            var area = new Rect(16f, 16f, width, height);

            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("Prototype Lab", GUI.skin.box);

            if (sceneName == Hub)
            {
                OpenButton("Survivors", Survivors);
                OpenButton("Roguelike", Roguelike);
                OpenButton("Hide and Seek", HideAndSeek);
                GUILayout.Label("Select a demo to play.");
            }
            else
            {
                GUILayout.Label(sceneName);
                GUILayout.Label("Demo workspace - add gameplay here.");
                OpenButton("Back to hub", Hub);
            }

            GUILayout.EndArea();
        }

        private static void OpenButton(string label, string sceneName)
        {
            if (GUILayout.Button(label, GUILayout.Height(32f)))
                SceneManager.LoadScene(sceneName);
        }
    }
}
