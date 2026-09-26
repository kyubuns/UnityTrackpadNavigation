using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;


namespace TrackpadNavigation.Tests
{
    // 製品側の座標変換を使わず、実際のOnGUI内で画面原点を計測する。
    internal sealed class WindowGuiProbe : IDisposable
    {
        readonly object host;
        readonly FieldInfo field;
        readonly Delegate original;
        readonly Delegate replacement;
        public Vector2? ScreenOrigin { get; private set; }

        public WindowGuiProbe(EditorWindow window)
        {
            host = EditorMember.Get(window, "m_Parent");
            for (var type = host.GetType(); type != null && field == null; type = type.BaseType)
            {
                field = type.GetField("m_OnGUI", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            }
            Assert.That(field, Is.Not.Null);
            original = (Delegate)field.GetValue(host);
            var invoke = (Action)Delegate.CreateDelegate(typeof(Action), original.Target, original.Method);
            Action probe = () =>
            {
                if (Event.current.type == EventType.Repaint)
                {
                    ScreenOrigin = GUIUtility.GUIToScreenPoint(Vector2.zero);
                }
                invoke();
            };
            replacement = Delegate.CreateDelegate(field.FieldType, probe.Target, probe.Method);
            field.SetValue(host, replacement);
        }

        public IEnumerator Wait(EditorWindow window)
        {
            for (int i = 0; i < 60 && !ScreenOrigin.HasValue; ++i)
            {
                window.Repaint();
                yield return null;
            }
            Assert.That(ScreenOrigin.HasValue, Is.True);
        }

        public void Dispose()
        {
            if (Equals(field.GetValue(host), replacement))
            {
                field.SetValue(host, original);
            }
        }
    }

    public sealed class WindowCoordinateTests
    {
        [UnityTest]
        public IEnumerator TimelineUsesGuiCoordinatesForHitTestingAndTimeAnchor()
        {
            var windowType = Type.GetType("UnityEditor.Timeline.TimelineWindow, Unity.Timeline.Editor");
            var assetType = Type.GetType("UnityEngine.Timeline.TimelineAsset, Unity.Timeline");
            if (windowType == null || assetType == null)
            {
                Assert.Ignore("Timeline is not installed.");
            }
            var asset = ScriptableObject.CreateInstance(assetType);
            var window = (EditorWindow)ScriptableObject.CreateInstance(windowType);
            try
            {
                window.Show();
                window.Focus();
                windowType.GetMethod("SetTimeline", new[] { assetType }).Invoke(window, new object[] { asset });
                yield return null;
                yield return null;
                using var probe = new WindowGuiProbe(window);
                yield return probe.Wait(window);
                var offset = probe.ScreenOrigin.Value - window.position.position;
                var target = TimelineNavigation.TryCreate(window);
                Assert.That(target, Is.Not.Null);
                Assert.That(target.IsAvailable, Is.True);
                var content = (Rect)EditorMember.Get(window, "sequenceContentRect");
                Assert.That(target.HitTest(offset + content.center), Is.True);
                Assert.That(target.HitTest(offset + new Vector2(content.center.x, content.yMin - 5)), Is.False);

                var state = EditorMember.Get(window, "state");
                EditorMember.Set(state, "timeAreaShownRange", new Vector2(0, 10));
                var range = (Vector2)EditorMember.Get(state, "timeAreaShownRange");
                var rect = (Rect)EditorMember.Get(EditorMember.Get(window, "timeArea"), "drawRect");
                const float fraction = 0.4f;
                var screen = probe.ScreenOrigin.Value + new Vector2(rect.x + rect.width * fraction, content.center.y);
                target.Apply(new TrackpadEvent
                {
                    Kind = GestureKind.Magnify, Magnification = 0.1, ScreenX = screen.x, ScreenY = screen.y
                }, new TrackpadPreferences());
                var next = (Vector2)EditorMember.Get(state, "timeAreaShownRange");
                Assert.That(next.y - next.x, Is.LessThan(range.y - range.x));
                Assert.That(Mathf.Lerp(next.x, next.y, fraction), Is.EqualTo(Mathf.Lerp(range.x, range.y, fraction)).Within(0.00001f));
            }
            finally
            {
                window.Close();
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [UnityTest]
        public IEnumerator AnimationUsesGuiCoordinatesInBothModes()
        {
            var previousSelection = Selection.activeObject;
            var clip = new AnimationClip();
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
            var window = ScriptableObject.CreateInstance<AnimationWindow>();
            try
            {
                Selection.activeObject = clip;
                window.Show();
                window.Focus();
                window.animationClip = clip;
                foreach (bool curves in new[] { false, true })
                {
                    var state = EditorMember.Get(window, "state");
                    var editor = EditorMember.Get(window, "animEditor");
                    EditorMember.Method(editor, curves ? "SwitchToCurveEditor" : "SwitchToDopeSheetEditor").Invoke(editor, null);
                    window.Repaint();
                    yield return null;
                    yield return null;
                    using var probe = new WindowGuiProbe(window);
                    yield return probe.Wait(window);
                    var offset = probe.ScreenOrigin.Value - window.position.position;
                    var area = EditorMember.Get(state, "timeArea");
                    var rect = (Rect)EditorMember.Get(area, "drawRect");
                    var target = AnimationNavigation.TryCreate(window);
                    Assert.That(target, Is.Not.Null);
                    Assert.That(target.IsAvailable, Is.True);
                    Assert.That(target.HitTest(offset + rect.center), Is.True);
                    Assert.That(target.HitTest(offset + new Vector2(rect.center.x, rect.yMin - 5)), Is.False);
                    var scale = (Vector2)EditorMember.Get(area, "scale");
                    var translation = (Vector2)EditorMember.Get(area, "translation");
                    var anchor = rect.size * 0.4f;
                    var screen = probe.ScreenOrigin.Value + rect.position + anchor;
                    var before = (anchor - translation) / scale;
                    target.Apply(new TrackpadEvent
                    {
                        Kind = GestureKind.Magnify, Magnification = 0.1, ScreenX = screen.x, ScreenY = screen.y
                    }, new TrackpadPreferences());
                    var nextScale = (Vector2)EditorMember.Get(area, "scale");
                    var after = (anchor - (Vector2)EditorMember.Get(area, "translation")) / nextScale;
                    Assert.That(nextScale.x, Is.GreaterThan(scale.x));
                    Assert.That(after.x, Is.EqualTo(before.x).Within(0.00001f));
                    if (curves)
                    {
                        Assert.That(after.y, Is.EqualTo(before.y).Within(0.00001f));
                    }
                }
            }
            finally
            {
                window.Close();
                Selection.activeObject = previousSelection;
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [UnityTest]
        public IEnumerator SceneViewAcceptsCameraAreaAtBottomEdge()
        {
            var window = ScriptableObject.CreateInstance<SceneView>();
            try
            {
                window.Show();
                yield return null;
                yield return null;
                EditorMember.Set(EditorMember.Get(window, "overlayCanvas"), "overlaysEnabled", false);
                window.Repaint();
                yield return null;
                var root = window.rootVisualElement;
                var point = new Vector2(root.worldBound.center.x, root.worldBound.yMax - 10);
                var picked = root.panel.Pick(point);
                Assert.That(picked, Is.Not.Null);
                Assert.That(NavigationHitTest.IsControl(picked, root), Is.False);
                Assert.That(point.y, Is.GreaterThan(window.position.height));
                var windowPoint = root.panel.visualTree.WorldToLocal(point);
                Assert.That(new SceneViewNavigation(window).HitTest(windowPoint), Is.True);
            }
            finally
            {
                window.Close();
            }
        }
    }
}
