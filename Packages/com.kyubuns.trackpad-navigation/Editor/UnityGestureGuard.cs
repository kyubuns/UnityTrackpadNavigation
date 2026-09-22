using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TrackpadNavigation
{
    internal static class UnityGestureGuard
    {
        const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
        static readonly FieldInfo Handler = typeof(EditorApplication).GetField("globalEventHandler", Flags);
        static readonly PropertyInfo MagnifyType = typeof(EditorGUIUtility).GetProperty("magnifyGestureEventType", Flags);
        static readonly EditorApplication.CallbackFunction Callback = SuppressMagnify;
        static EventType magnifyType;
        public static bool Installed
        {
            get; private set;
        }
        public static int Suppressed
        {
            get; private set;
        }

        public static void Install()
        {
            if (Installed)
            {
                return;
            }

            if (Handler?.FieldType != typeof(EditorApplication.CallbackFunction) || MagnifyType?.PropertyType != typeof(EventType))
            {
                throw new InvalidOperationException("Unity's pinch event hook is unavailable.");
            }

            magnifyType = (EventType)MagnifyType.GetValue(null);
            Handler.SetValue(null, Delegate.Combine((Delegate)Handler.GetValue(null), Callback));
            Installed = true;
        }

        public static void Remove()
        {
            if (!Installed)
            {
                return;
            }

            Handler.SetValue(null, Delegate.Remove((Delegate)Handler.GetValue(null), Callback));
            Installed = false;
        }

        static void SuppressMagnify()
        {
            if (!TrackpadInput.Running || !TrackpadSettings.Current.Enabled || Event.current?.type != magnifyType)
            {
                return;
            }
            // AppKit monitorで止まらないUnity内部のピンチも、MaximizeGestureHandlerより前で消費する。
            // ナビゲーションはNative入力だけで処理し、二重にズームしない。
            Event.current.Use();
            ++Suppressed;
        }
    }
}
