using UnityEditor;
using UnityEditor.SceneManagement;
namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumFrameChecks
    {
        public static void Run()
        {
            AquariumHotPathChecks.Run();
            EditorSceneManager.OpenScene("Assets/Demos/HideAndSeek/30_HideAndSeek.unity");
            EditorApplication.EnterPlaymode();
        }
    }
}