using UnityEditor;
using UnityEngine;

namespace TrackpadNavigation
{
    internal sealed class TrackpadDebugWindow : EditorWindow
    {
        [MenuItem("Window/Trackpad Navigation/Diagnostics")]
        public static void Open() => GetWindow<TrackpadDebugWindow>("Trackpad Diagnostics");
        void OnEnable() => EditorApplication.update += Refresh;
        void OnDisable()
        {
            EditorApplication.update -= Refresh;
            TrackpadNavigator.Observe = false;
        }
        double nextRepaint;
        void Refresh()
        {
            if (EditorApplication.timeSinceStartup < nextRepaint)
            {
                return;
            }

            nextRepaint = EditorApplication.timeSinceStartup + 0.1;
            Repaint();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Trackpad Navigation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(TrackpadInput.Status, TrackpadInput.Running ? MessageType.Info : MessageType.Warning);
            TrackpadNavigator.Observe = EditorGUILayout.Toggle("Observe all Editor gestures", TrackpadNavigator.Observe);
            EditorGUILayout.LabelField("Target", TrackpadNavigator.TargetDescription);
            if (!string.IsNullOrEmpty(TrackpadNavigator.LastError))
            {
                EditorGUILayout.HelpBox(TrackpadNavigator.LastError, MessageType.Warning);
            }

            var value = TrackpadNavigator.LastEvent;
            EditorGUILayout.LabelField("Pan X / Y (points)", $"{value.DeltaX:F4} / {value.DeltaY:F4}");
            EditorGUILayout.LabelField("Magnification", value.Magnification.ToString("F6"));
            EditorGUILayout.LabelField("Rotation (degrees)", value.Rotation.ToString("F4"));
            EditorGUILayout.LabelField("Gesture phase", value.Phase.ToString());
            EditorGUILayout.LabelField("Momentum phase", value.MomentumPhase.ToString());
            EditorGUILayout.LabelField("Modifier keys", value.Modifiers.ToString());
            EditorGUILayout.LabelField("Kind / flags", $"{value.Kind} / {value.Flags}");
            EditorGUILayout.LabelField("Screen position", value.ScreenPosition.ToString("F1"));
            EditorGUILayout.LabelField("Sequence / native window", $"{value.Sequence} / {value.WindowNumber}");
            var stats = TrackpadInput.Stats;
            EditorGUILayout.LabelField("Received / captured / overflow", $"{stats.Received} / {stats.Captured} / {stats.Overflow}");
            EditorGUILayout.LabelField("Queue / monitor", $"{stats.Queued} / {stats.Installed}");
            EditorGUILayout.LabelField("Unity pinch guard / blocked", $"{UnityGestureGuard.Installed} / {UnityGestureGuard.Suppressed}");
            EditorGUILayout.HelpBox("Observation does not consume input outside supported canvases. Physical trackpad gestures are needed to judge feel.", MessageType.None);
            if (GUILayout.Button("Restart native monitor"))
            {
                TrackpadInput.Stop();
                TrackpadInput.Start();
            }
            if (GUILayout.Button("Copy diagnostic report"))
            {
                EditorGUIUtility.systemCopyBuffer = Report();
            }
        }

        public static string Report()
        {
            var stats = TrackpadInput.Stats;
            var value = TrackpadNavigator.LastEvent;
            var version = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TrackpadDebugWindow).Assembly)?.version ?? "local";
            return $"Trackpad Navigation {version}\nUnity {Application.unityVersion}\n{SystemInfo.operatingSystem}\n{TrackpadInput.Status}\nTarget: {TrackpadNavigator.TargetDescription}\nInput: {value.Kind}, phase={value.Phase}, momentum={value.MomentumPhase}, modifiers={value.Modifiers}\nDelta: {value.DeltaX}, {value.DeltaY}; magnification={value.Magnification}; rotation={value.Rotation}\nReceived={stats.Received}, captured={stats.Captured}, overflow={stats.Overflow}, queued={stats.Queued}, installed={stats.Installed}\nUnity pinch guard={UnityGestureGuard.Installed}, blocked={UnityGestureGuard.Suppressed}\nLast error: {TrackpadNavigator.LastError}";
        }
    }
}
