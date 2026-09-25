using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TrackpadNavigation
{
    internal sealed class GameViewNavigation : ZoomAreaNavigation
    {
        readonly MethodInfo constrain;
        public override string Description => "Game View — pan / cursor zoom";

        GameViewNavigation(EditorWindow window, object area, MethodInfo constrain) : base(window, area)
        {
            this.constrain = constrain;
        }

        public static INavigationTarget TryCreate(EditorWindow window)
        {
            var area = EditorMember.Get(window, "m_ZoomArea");
            var constrain = EditorMember.Method(window, "EnforceZoomAreaConstraints");
            if (!SupportsArea(area) || constrain == null ||
                !(EditorMember.Get(window, "minScale") is float) || !(EditorMember.Get(window, "maxScale") is float))
            {
                return null;
            }
            return new GameViewNavigation(window, area, constrain);
        }

        // 再生中はゲーム入力を優先する。取得済みの入力先も再生開始時点で無効にする。
        protected override bool IsCurrent => (!EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPaused) &&
            ReferenceEquals(Area, EditorMember.Get(Window, "m_ZoomArea"));

        protected override Vector2 ZoomScale(Vector2 scale, float factor)
        {
            float minimum = (float)EditorMember.Get(Window, "minScale");
            float maximum = (float)EditorMember.Get(Window, "maxScale");
            return Vector2.one * Mathf.Clamp(scale.x * factor, minimum, maximum);
        }

        // Game View固有の画像端へのクランプも即時適用する。
        protected override void AfterApply() => constrain.Invoke(Window, null);
    }
}
