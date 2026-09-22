using UnityEditor;
using UnityEngine;

namespace TrackpadNavigation
{
    internal static class TrackpadSettingsProvider
    {
        [SettingsProvider]
        static SettingsProvider Create() => new SettingsProvider("Preferences/Trackpad Navigation", SettingsScope.User)
        {
            label = "Trackpad Navigation",
            keywords = new[] {
                "trackpad",
                "pan",
                "zoom",
                "orbit",
                "mac",
                "look",
                "momentum"
            },
            guiHandler = _ => Draw()
        };

        static void Draw()
        {
            var value = TrackpadSettings.Current;
            EditorGUI.BeginChangeCheck();
            value.Enabled = EditorGUILayout.Toggle("Enable", value.Enabled);
            EditorGUILayout.HelpBox("Two fingers: Pan · Pinch: Zoom\nScene: Option + two fingers: Orbit · Command + two fingers: Look around", MessageType.Info);
            value.PanSensitivity = EditorGUILayout.Slider("Pan sensitivity", value.PanSensitivity, 0.05f, 5);
            value.ZoomSensitivity = EditorGUILayout.Slider("Zoom sensitivity", value.ZoomSensitivity, 0.05f, 5);
            value.OrbitSensitivity = EditorGUILayout.Slider("Orbit / look sensitivity", value.OrbitSensitivity, 0.05f, 5);
            value.InvertNaturalScrolling = EditorGUILayout.Toggle("Invert macOS scroll direction", value.InvertNaturalScrolling);
            value.InvertZoom = EditorGUILayout.Toggle("Invert zoom", value.InvertZoom);
            value.InvertPanX = EditorGUILayout.Toggle("Invert pan X", value.InvertPanX);
            value.InvertPanY = EditorGUILayout.Toggle("Invert pan Y", value.InvertPanY);
            value.InvertOrbitX = EditorGUILayout.Toggle("Invert rotation X", value.InvertOrbitX);
            value.InvertOrbitY = EditorGUILayout.Toggle("Invert rotation Y", value.InvertOrbitY);
            value.Momentum = EditorGUILayout.Toggle("Enable momentum", value.Momentum);
            value.SceneIntegration = EditorGUILayout.Toggle("Scene View integration", value.SceneIntegration);
            value.GraphIntegration = EditorGUILayout.Toggle("Graph / Timeline integration", value.GraphIntegration);
            if (EditorGUI.EndChangeCheck())
            {
                TrackpadSettings.Save();
            }

            if (GUILayout.Button("Restore defaults", GUILayout.Width(160)))
            {
                TrackpadSettings.Reset();
            }

            if (GUILayout.Button("Open diagnostics", GUILayout.Width(160)))
            {
                TrackpadDebugWindow.Open();
            }
        }
    }
}
