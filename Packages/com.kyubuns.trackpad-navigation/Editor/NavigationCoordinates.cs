using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrackpadNavigation
{
    internal static class NavigationCoordinates
    {
        // EditorWindow.positionの原点はタブや枠を含むホスト側。EditorWindowのrootを使うとヘッダー分を二重に加算する。
        static VisualElement WindowRoot(EditorWindow window) => window.rootVisualElement.panel.visualTree;

        public static Vector2 ToPanel(EditorWindow window, Vector2 windowPoint) => WindowRoot(window).LocalToWorld(windowPoint);
        public static Vector2 ToLocal(EditorWindow window, VisualElement element, Vector2 windowPoint) => element.WorldToLocal(ToPanel(window, windowPoint));
        public static Vector2 ToWindow(EditorWindow window, VisualElement element, Vector2 elementPoint) => WindowRoot(window).WorldToLocal(element.LocalToWorld(elementPoint));
    }
}
