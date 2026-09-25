using UnityEditor;

namespace TrackpadNavigation
{
    internal sealed class CurveNavigation : ZoomAreaNavigation
    {
        public override string Description => "Curve Editor — time / value pan / cursor zoom";
        CurveNavigation(EditorWindow window, object area) : base(window, area)
        {
        }

        public static INavigationTarget TryCreate(EditorWindow window)
        {
            var area = EditorMember.Get(window, "m_CurveEditor");
            return SupportsArea(area) ? new CurveNavigation(window, area) : null;
        }

        protected override bool IsCurrent => ReferenceEquals(Area, EditorMember.Get(Window, "m_CurveEditor"));
    }
}
