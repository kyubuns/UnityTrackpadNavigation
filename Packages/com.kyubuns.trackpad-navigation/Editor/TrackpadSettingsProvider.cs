using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TrackpadNavigation
{
    internal static class TrackpadSettingsProvider
    {
        static readonly PropertyInfo ShaderGraphZoomStepSize = Type.GetType("UnityEditor.ShaderGraph.ShaderGraphPreferences, Unity.ShaderGraph.Editor")?.GetProperty("zoomStepSize", BindingFlags.Static | BindingFlags.NonPublic);

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
                "momentum",
                "Shader Graph",
                "VFX Graph",
                "UI Builder",
                "Zoom Step Size"
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
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("VFX Graph", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Controls VFX Graph's standard scroll zoom, including Control + two-finger scrolling. Lower values zoom more slowly. Requires Enable and Graph / Timeline integration. Pinch uses Zoom sensitivity above.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!value.Enabled || !value.GraphIntegration))
            {
                value.VfxZoomStepSize = EditorGUILayout.Slider(new GUIContent("Zoom Step Size", "Trackpad Navigation override. Restore defaults resets this to Unity's standard scroll step."), value.VfxZoomStepSize, 0.001f, 1);
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UI Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Controls UI Builder's standard scroll zoom, including Control + two-finger scrolling. Lower values zoom more slowly. Requires Enable and Graph / Timeline integration. Pinch uses Zoom sensitivity above.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!value.Enabled || !value.GraphIntegration))
            {
                value.BuilderZoomStepSize = EditorGUILayout.Slider(new GUIContent("Zoom Step Size", "Trackpad Navigation override. Default: 0.05 (Unity's standard zoom levels)."), value.BuilderZoomStepSize, 0.001f, 1);
            }
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

            DrawUnitySettings();
        }

        static void DrawUnitySettings()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Unity standard settings — Shader Graph", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Changes Unity's Preferences > Shader Graph > Zoom Step Size. Controls standard scroll zoom, including Control + two-finger scrolling. This is separate from Trackpad Navigation's pinch sensitivity and Restore defaults.", MessageType.Info);
            if (ShaderGraphZoomStepSize == null || ShaderGraphZoomStepSize.PropertyType != typeof(float) || !ShaderGraphZoomStepSize.CanRead || !ShaderGraphZoomStepSize.CanWrite)
            {
                EditorGUILayout.HelpBox("Zoom Step Size is unavailable in this Shader Graph version, or Shader Graph is not installed.", MessageType.None);
                return;
            }

            // EditorPrefsだけではキャッシュと開いているGraphへ反映されないため、Unity自身のsetterを使う。
            float current = (float)ShaderGraphZoomStepSize.GetValue(null);
            EditorGUI.BeginChangeCheck();
            float next = EditorGUILayout.Slider(new GUIContent("Zoom Step Size", "Unity standard setting. Lower values zoom more slowly. Default: 0.5."), current, 0, 1);
            if (EditorGUI.EndChangeCheck())
            {
                ShaderGraphZoomStepSize.SetValue(null, next);
            }
        }
    }
}
