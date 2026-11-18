using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Riftbound.Editor
{
    public static class StartupProbe
    {
        [MenuItem("Riftbound/Validate Play Mode startup")]
        public static void Validate()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("Enter Play Mode before validating startup.");
                return;
            }
            if (EditorApplication.isPaused)
            {
                Debug.LogWarning("Unity is paused. Press Pause again to resume gameplay.");
                return;
            }

            var player = Object.FindFirstObjectByType<PlayerController>();
            var camera = Object.FindFirstObjectByType<OrbitCamera>();
            var hud = Object.FindFirstObjectByType<PrototypeHUD>();
            var frontend = Object.FindFirstObjectByType<FrontendMenu>();
            bool connected = player && player.Stats && player.animator && camera &&
                camera.target == player.transform && hud && hud.player == player && frontend;
            bool playable = FrontendMenu.IsOpen || (Time.timeScale > 0 && camera && !camera.Paused);
            if (!connected || !playable || Keyboard.current == null)
            {
                Debug.LogError($"Startup check failed: player/camera/HUD connected={connected}, menu or controls available={playable}, keyboard={Keyboard.current != null}.");
                return;
            }

            Debug.Log($"Startup check passed: {player.name}, menu={FrontendMenu.IsOpen}, time scale={Time.timeScale}.");
        }
    }
}
