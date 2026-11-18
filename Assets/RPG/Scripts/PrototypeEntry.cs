using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riftbound
{
    public static class PrototypeEntry
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OpenCurrentAdventure()
        {
            string scene = SceneManager.GetActiveScene().name;
            if (scene != "Outpost" && scene != "Outpost-Updated") return;
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player && Application.CanStreamedLevelBeLoaded("Outpost-Expanded"))
                SceneManager.LoadScene("Outpost-Expanded");
            else if (scene == "Outpost" && player && Application.CanStreamedLevelBeLoaded("Outpost-Updated"))
                SceneManager.LoadScene("Outpost-Updated");
        }
    }
}
