using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace TrackpadNavigation.Tests
{
    public sealed class CurveNavigationTests
    {
        [UnityTest]
        public IEnumerator InspectorCurveWindowPansAndZoomsWithoutEditingKeys()
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.CurveEditorWindow", true);
            var settingsType = typeof(EditorWindow).Assembly.GetType("UnityEditor.CurveEditorSettings", true);
            var window = (EditorWindow)type.GetProperty("instance").GetValue(null);
            var curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            var keys = curve.keys;
            int changes = 0;
            try
            {
                type.GetMethod("Show", new[]
                {
                    typeof(Action<AnimationCurve>), settingsType
                }).Invoke(window, new object[]
                {
                    new Action<AnimationCurve>(_ => ++changes), null
                });
                type.GetProperty("curve").SetValue(null, curve);
                type.GetMethod("FrameClip").Invoke(window, null);
                yield return null;
                yield return null;

                var editor = EditorMember.Get(window, "m_CurveEditor");
                var rect = (Rect)EditorMember.Get(editor, "drawRect");
                var target = NavigationTargets.Resolve(window, rect.center, new TrackpadPreferences());
                Assert.That(target, Is.TypeOf<CurveNavigation>());
                Assert.That(NavigationTargets.Resolve(window, rect.center, new TrackpadPreferences
                {
                    GraphIntegration = false
                }), Is.Null);
                Assert.That(target.HitTest(rect.center), Is.True);
                Assert.That(target.HitTest(new Vector2(rect.center.x, window.position.height - 25)), Is.False);
                Assert.That(target.HitTest(new Vector2(rect.xMax + 1, rect.center.y)), Is.False);
                var selection = EditorMember.Get(editor, "selectedCurves");
                var translation = (Vector2)EditorMember.Get(editor, "translation");
                var scale = (Vector2)EditorMember.Get(editor, "scale");
                var preferences = new TrackpadPreferences();
                target.Apply(new TrackpadEvent
                {
                    Kind = GestureKind.Scroll, DeltaX = 3.25, DeltaY = -2.5
                }, preferences);
                var panned = (Vector2)EditorMember.Get(editor, "translation");
                Assert.That(Vector2.Distance(panned, translation + new Vector2(3.25f, -2.5f)), Is.LessThan(0.001f));

                var local = rect.position + rect.size * 0.4f;
                var screen = window.position.position + local;
                var anchor = local - rect.position;
                var point = (anchor - panned) / scale;
                var pinch = new TrackpadEvent
                {
                    Kind = GestureKind.Magnify, Magnification = 0.12, ScreenX = screen.x, ScreenY = screen.y
                };
                target.Apply(pinch, preferences);
                var zoomedScale = (Vector2)EditorMember.Get(editor, "scale");
                var zoomedTranslation = (Vector2)EditorMember.Get(editor, "translation");
                Assert.That(zoomedScale.x, Is.GreaterThan(scale.x));
                Assert.That(Vector2.Distance((anchor - zoomedTranslation) / zoomedScale, point), Is.LessThan(0.0001f));
                pinch.Magnification = -pinch.Magnification;
                target.Apply(pinch, preferences);
                Assert.That(Vector2.Distance((Vector2)EditorMember.Get(editor, "scale"), scale), Is.LessThan(0.001f));
                Assert.That(Vector2.Distance((Vector2)EditorMember.Get(editor, "translation"), panned), Is.LessThan(0.001f));
                yield return null;
                Assert.That(curve.keys, Is.EqualTo(keys));
                Assert.That(EditorMember.Get(editor, "selectedCurves"), Is.SameAs(selection));
                Assert.That(changes, Is.Zero);
                window.Close();
                Assert.That(target.HitTest(rect.center), Is.False);
                Assert.DoesNotThrow(() => target.Apply(pinch, preferences));
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
