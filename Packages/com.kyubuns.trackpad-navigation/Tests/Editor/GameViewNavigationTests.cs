using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace TrackpadNavigation.Tests
{
    public sealed class GameViewNavigationTests
    {
        [UnityTest]
        public IEnumerator GameViewKeepsZoomAnchorAndClampsToImage()
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView", true);
            var window = (EditorWindow)ScriptableObject.CreateInstance(type);
            try
            {
                window.Show();
                yield return null;
                yield return null;
                using var probe = new WindowGuiProbe(window);
                yield return probe.Wait(window);
                var origin = probe.ScreenOrigin.Value;
                var offset = origin - window.position.position;
                var area = EditorMember.Get(window, "m_ZoomArea");
                var rect = (Rect)EditorMember.Get(area, "drawRect");
                var target = NavigationTargets.Resolve(window, offset + rect.center, new TrackpadPreferences());
                Assert.That(target, Is.TypeOf<GameViewNavigation>());
                Assert.That(target.HitTest(offset + rect.center), Is.True);
                Assert.That(target.HitTest(offset + new Vector2(rect.center.x, 5)), Is.False);
                Assert.That(NavigationTargets.Resolve(window, offset + rect.center, new TrackpadPreferences
                {
                    GraphIntegration = false
                }), Is.Null);

                var settings = new TrackpadPreferences();
                var screen = origin + rect.center;
                var pinch = new TrackpadEvent
                {
                    Kind = GestureKind.Magnify, Magnification = 0.7, ScreenX = screen.x, ScreenY = screen.y
                };
                target.Apply(pinch, settings);
                yield return null;
                rect = (Rect)EditorMember.Get(area, "drawRect");
                var scale = (Vector2)EditorMember.Get(area, "scale");
                var translation = (Vector2)EditorMember.Get(area, "translation");
                target.Apply(new TrackpadEvent
                {
                    Kind = GestureKind.Scroll, DeltaX = 3.5, DeltaY = -2.25
                }, settings);
                var panned = (Vector2)EditorMember.Get(area, "translation");
                Assert.That(Vector2.Distance(panned, translation + new Vector2(3.5f, -2.25f)), Is.LessThan(0.001f));
                var anchor = rect.size * 0.4f;
                screen = origin + rect.position + anchor;
                pinch.ScreenX = screen.x;
                pinch.ScreenY = screen.y;
                pinch.Magnification = 0.1;
                var point = (anchor - panned) / scale;
                target.Apply(pinch, settings);
                var zoomedScale = (Vector2)EditorMember.Get(area, "scale");
                var zoomedTranslation = (Vector2)EditorMember.Get(area, "translation");
                Assert.That(Vector2.Distance((anchor - zoomedTranslation) / zoomedScale, point), Is.LessThan(0.001f));
                pinch.Magnification = 100;
                target.Apply(pinch, settings);
                Assert.That(((Vector2)EditorMember.Get(area, "scale")).x, Is.EqualTo((float)EditorMember.Get(window, "maxScale")).Within(0.001f));
                pinch.Magnification = -100;
                for (int i = 0; i < 4; ++i)
                {
                    target.Apply(pinch, settings);
                }
                Assert.That(((Vector2)EditorMember.Get(area, "scale")).x, Is.EqualTo((float)EditorMember.Get(window, "minScale")).Within(0.001f));
                target.Apply(new TrackpadEvent
                {
                    Kind = GestureKind.Scroll, DeltaX = 100000, DeltaY = -100000
                }, settings);
                var shown = (Rect)EditorMember.Get(area, "shownArea");
                Assert.That(shown.center.magnitude, Is.LessThan(0.001f));
                window.Close();
                Assert.That(target.HitTest(offset + rect.center), Is.False);
            }
            finally
            {
                if (window != null)
                {
                    window.Close();
                }
            }
        }
    }
}
