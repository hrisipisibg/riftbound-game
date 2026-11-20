using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    static class FrontierSitePreview
    {
        static GameObject westernPreview;
        static GameObject cavePreview;

        static FrontierSitePreview()
        {
            EditorSceneManager.sceneOpened += (_, _) => Queue();
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) Remove();
                if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode) Queue();
            };
            AssemblyReloadEvents.beforeAssemblyReload += Remove;
            Queue();
        }

        static void Queue()
        {
            EditorApplication.delayCall -= Show;
            EditorApplication.delayCall += Show;
        }

        [MenuItem("Riftbound/World/Show frontier sites in Scene view")]
        static void Show()
        {
            if (!WorldRegions.Expanded) return;
            if (EditorApplication.isPlaying)
            {
                InstallInCurrentPlaySession();
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Remove();
            westernPreview = WesternRoadCamps.CreateScenePreview();
            cavePreview = BrassHollows.CreateScenePreview();
            SceneView.RepaintAll();
        }

        static void InstallInCurrentPlaySession()
        {
            var hud = Object.FindAnyObjectByType<PrototypeHUD>();
            var player = hud && hud.player ? hud.player : Object.FindAnyObjectByType<PlayerController>();
            if (!player) return;
            WesternRoadCamps.Ensure(player);
            BrassHollows.Ensure();
            if (WesternRoadCamps.Current && BrassHollows.Current)
                Debug.Log("[Riftbound] Frontier sites are present in this play session. " +
                    $"Split-Seal Layby {WesternRoadCamps.Current.Layby.position}; " +
                    $"Tallykeepers’ Rest {WesternRoadCamps.Current.Rest.position}; " +
                    $"Brass Hollows {BrassHollows.Current.Entrance}.");
        }

        [MenuItem("Riftbound/World/Frame Split-Seal Layby")]
        static void FrameLayby() => FrameSite("Split-Seal Layby");

        [MenuItem("Riftbound/World/Frame Tallykeepers’ Rest")]
        static void FrameRest() => FrameSite("Tallykeepers’ Rest");

        [MenuItem("Riftbound/World/Frame Brass Hollows entrance")]
        static void FrameCave() => FrameSite("Mine mouth");

        [MenuItem("Riftbound/World/Teleport to Brass Hollows entrance (Play Mode)", true)]
        static bool CanTeleportToCave() =>
            EditorApplication.isPlaying && WorldRegions.Expanded && !FrontendMenu.IsOpen;

        [MenuItem("Riftbound/World/Teleport to Brass Hollows entrance (Play Mode)")]
        static void TeleportToCave()
        {
            var hud = Object.FindAnyObjectByType<PrototypeHUD>();
            var player = hud && hud.player ? hud.player : Object.FindAnyObjectByType<PlayerController>();
            if (!player || !player.Stats || !player.Stats.Alive) return;
            BrassHollows.Ensure();
            var cave = BrassHollows.Current;
            var motor = player.GetComponent<CharacterController>();
            if (!cave || !motor || !motor.enabled) return;
            Vector3 approach = cave.Entrance - Vector3.forward * 2.15f;
            approach.y = WorldRegions.GroundHeight(approach) + .20f;
            player.MapOpen = false;
            if (player.Mounted) player.Dismount();
            if (player.TravelBrassHollows(approach, Quaternion.identity, savePosition: false))
            {
                player.Message = "Brass Hollows entrance. Press E to enter once Cut the Siphon is active.";
                Debug.Log($"[Riftbound] Teleported the player to the Brass Hollows entrance at {player.transform.position}.");
            }
        }

        static void FrameSite(string name)
        {
            Show();
            var western = EditorApplication.isPlaying && WesternRoadCamps.Current
                ? WesternRoadCamps.Current.transform : westernPreview ? westernPreview.transform : null;
            var cave = EditorApplication.isPlaying && BrassHollows.Current
                ? BrassHollows.Current.transform : cavePreview ? cavePreview.transform : null;
            var site = name == "Mine mouth" ? (cave ? cave.Find(name) : null) : (western ? western.Find(name) : null);
            if (!site) return;
            Selection.activeGameObject = site.gameObject;
            var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            view.Focus();
            view.LookAt(site.position + Vector3.up * 1.25f, Quaternion.Euler(22f, site.eulerAngles.y + 20f, 0f), 8f);
        }

        static void Remove()
        {
            if (westernPreview) Object.DestroyImmediate(westernPreview);
            if (cavePreview) Object.DestroyImmediate(cavePreview);
            westernPreview = cavePreview = null;
        }
    }
}
