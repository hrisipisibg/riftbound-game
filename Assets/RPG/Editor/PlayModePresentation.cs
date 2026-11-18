using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class PlayModePresentation
    {
        static PlayModePresentation() { EditorApplication.playModeStateChanged += Changed; }
        static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += HideGizmos;
        }
        [MenuItem("Riftbound/Hide gameplay light and camera icons")]
        public static void HideGizmos()
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (type == null) return;
            var property = type.GetProperty("drawGizmos",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            foreach (var view in Resources.FindObjectsOfTypeAll(type))
                if (property != null && property.CanWrite) property.SetValue(view,false);
        }
    }
}
