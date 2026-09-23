using System;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace TrackpadNavigation.Development
{
    internal sealed class TrackpadLiveViewWindow : EditorWindow
    {
        [Flags]
        enum Modifiers
        {
            Option = 1, Command = 2, Shift = 4, Control = 8
        }

        [StructLayout(LayoutKind.Sequential)]
        struct TouchState
        {
            public int Count;
            public Modifiers Keys;
            public float Width, Height;
            public ulong Sequence;
        }

        readonly Vector2[] touches = new Vector2[10];
        TouchState state;
        bool running;
        double nextRefresh;
        string error;
        GUIStyle caption, keyStyle;
        static readonly Color Background = new Color32(19, 22, 28, 255);
        static readonly Color Surface = new Color32(29, 34, 42, 255);
        static readonly Color Finger = new Color32(113, 239, 196, 255);

        [MenuItem("Window/Trackpad Navigation/Live View")]
        public static void Open()
        {
            var window = GetWindow<TrackpadLiveViewWindow>(utility: true, title: "Trackpad Live View");
            window.minSize = new Vector2(220, 180);
            window.Show();
        }

        void OnEnable()
        {
            minSize = new Vector2(220, 180);
            if (position.width > 600 || position.height > 600)
            {
                position = new Rect(position.position, new Vector2(320, 245));
            }
            try
            {
                if (RuntimeInformation.ProcessArchitecture != Architecture.Arm64)
                {
                    error = "Apple Silicon is required.";
                    return;
                }
                running = TL_Start(Marshal.SizeOf<TouchState>()) != 0;
                if (!running)
                {
                    error = "Live View could not start.";
                }
            }
            catch (Exception exception) when (exception is DllNotFoundException || exception is EntryPointNotFoundException || exception is BadImageFormatException)
            {
                error = exception.Message;
            }
            EditorApplication.update += Refresh;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
        }

        void OnDisable()
        {
            EditorApplication.update -= Refresh;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorApplication.quitting -= Stop;
            Stop();
        }

        void Stop()
        {
            if (running)
            {
                TL_Stop();
                running = false;
            }
        }

        void Refresh()
        {
            if (!running || EditorApplication.timeSinceStartup < nextRefresh)
            {
                return;
            }
            nextRefresh = EditorApplication.timeSinceStartup + 1.0 / 60;
            TL_Poll(out state, touches, touches.Length);
            Repaint();
        }

        void OnGUI()
        {
            EditorGUI.DrawRect(new Rect(Vector2.zero, position.size), Background);
            if (!string.IsNullOrEmpty(error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Warning);
                return;
            }
            if (caption == null)
            {
                caption = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 10,
                    normal =
                    {
                        textColor = new Color32(150, 162, 178, 255)
                    }
                };
                keyStyle = new GUIStyle(caption)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12
                };
            }
            GUI.Label(new Rect(18, 8, position.width - 36, 22), "TRACKPAD", caption);
            float aspect = state.Height > 0 ? state.Width / state.Height : 1.6f;
            float width = Mathf.Min(position.width - 36, (position.height - 76) * aspect);
            float height = width / aspect;
            var pad = new Rect((position.width - width) / 2, 36 + (position.height - 76 - height) / 2, width, height);
            RoundedRect(pad, new Color32(66, 76, 90, 255), 12);
            RoundedRect(new Rect(pad.x + 1, pad.y + 1, pad.width - 2, pad.height - 2), Surface, 11);
            for (int index = 0; index < state.Count; ++index)
            {
                // NSTouchは左下原点。位置は加工せず、描画のY軸だけ反転する。
                Vector2 point = touches[index];
                var center = new Vector2(pad.x + Mathf.Clamp01(point.x) * pad.width, pad.yMax - Mathf.Clamp01(point.y) * pad.height);
                RoundedRect(new Rect(center.x - 13, center.y - 13, 26, 26), new Color(Finger.r, Finger.g, Finger.b, 0.15f), 13);
                RoundedRect(new Rect(center.x - 7, center.y - 7, 14, 14), Finger, 7);
            }
            float keyWidth = Mathf.Min(65, (position.width - 36) / 4);
            float left = (position.width - keyWidth * 4) / 2;
            DrawKey(new Rect(left, position.height - 32, keyWidth - 4, 22), "⌥", Modifiers.Option);
            DrawKey(new Rect(left + keyWidth, position.height - 32, keyWidth - 4, 22), "⌘", Modifiers.Command);
            DrawKey(new Rect(left + keyWidth * 2, position.height - 32, keyWidth - 4, 22), "⇧", Modifiers.Shift);
            DrawKey(new Rect(left + keyWidth * 3, position.height - 32, keyWidth - 4, 22), "⌃", Modifiers.Control);
        }

        void DrawKey(Rect rect, string label, Modifiers modifier)
        {
            bool pressed = (state.Keys & modifier) != 0;
            RoundedRect(rect, pressed ? Finger : Surface, 5);
            keyStyle.normal.textColor = pressed ? Background : new Color32(113, 127, 145, 255);
            GUI.Label(rect, label, keyStyle);
        }

        static void RoundedRect(Rect rect, Color color, float radius)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, radius);
        }

        [DllImport("TrackpadLiveView", CallingConvention = CallingConvention.Cdecl)] static extern int TL_Start(int stateSize);
        [DllImport("TrackpadLiveView", CallingConvention = CallingConvention.Cdecl)] static extern void TL_Stop();
        [DllImport("TrackpadLiveView", CallingConvention = CallingConvention.Cdecl)] static extern int TL_Poll(out TouchState state, [Out] Vector2[] touches, int capacity);
    }
}
