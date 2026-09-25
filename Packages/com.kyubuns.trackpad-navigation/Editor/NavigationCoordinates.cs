using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrackpadNavigation
{
    internal static class NavigationCoordinates
    {
        // Overlay対応ウィンドウのrootVisualElementはツールバーより下に配置される。
        // EditorWindow.positionからの座標は、コンテンツではなくウィンドウ全体のルートを基準にする。
        static VisualElement WindowRoot(EditorWindow window) => EditorMember.Get(window, "baseRootVisualElement") as VisualElement ?? window.rootVisualElement;

        public static Vector2 ToPanel(EditorWindow window, Vector2 windowPoint) => WindowRoot(window).LocalToWorld(windowPoint);
        public static Vector2 ToLocal(EditorWindow window, VisualElement element, Vector2 windowPoint) => element.WorldToLocal(ToPanel(window, windowPoint));
        public static Vector2 ToWindow(EditorWindow window, VisualElement element, Vector2 elementPoint) => WindowRoot(window).WorldToLocal(element.LocalToWorld(elementPoint));
    }
}
