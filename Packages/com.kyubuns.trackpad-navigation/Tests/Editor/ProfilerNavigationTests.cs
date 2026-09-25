using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TrackpadNavigation.Tests
{
    public sealed class ProfilerNavigationTests
    {
        [UnityTest]
        public IEnumerator CpuTimelineUsesDetailsCoordinatesAndPreservesFrameSelection()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            bool originalRecording = ProfilerDriver.enabled;
            bool originalProfileEditor = ProfilerDriver.profileEditor;
            var window = ScriptableObject.CreateInstance<ProfilerWindow>();
            var record = typeof(ProfilerWindow).GetMethod("SetRecordingEnabled", flags);
            try
            {
                window.Show();
                window.position = new Rect(100, 100, 1000, 700);
                var modules = (IEnumerable)EditorMember.Get(window, "m_AllModules");
                var module = modules.Cast<object>().Single(m => m.GetType().Name == "CPUProfilerModule");
                EditorMember.Set(window, "selectedModule", module);
                var viewType = module.GetType().GetProperty("ViewType", flags);
                viewType.SetValue(module, Enum.Parse(viewType.PropertyType, "Timeline"));
                ProfilerDriver.profileEditor = true;
                record.Invoke(window, new object[] { true });
                for (int i = 0; i < 10; ++i)
                {
                    yield return null;
                }
                record.Invoke(window, new object[] { false });
                ProfilerDriver.profileEditor = false;
                Assert.That(ProfilerDriver.lastFrameIndex, Is.GreaterThanOrEqualTo(0));
                window.selectedFrameIndex = ProfilerDriver.lastFrameIndex;
                window.Repaint();
                yield return null;
                yield return null;
                var timeline = EditorMember.Get(module, "m_TimelineGUI");
                var area = EditorMember.Get(timeline, "m_TimeArea");
                Assert.That(area, Is.Not.Null);
                var controller = EditorMember.Get(module, "m_DetailsViewController");
                var canvas = (VisualElement)EditorMember.Get(controller, "m_LegacyIMGUIView");
                var rect = (Rect)EditorMember.Get(area, "drawRect");
                var point = window.rootVisualElement.WorldToLocal(canvas.LocalToWorld(rect.center));
                var target = NavigationTargets.Resolve(window, point, new TrackpadPreferences());
                Assert.That(target, Is.TypeOf<ProfilerNavigation>());
                Assert.That(target.HitTest(point), Is.True);
                Assert.That(target.HitTest(new Vector2(point.x, 10)), Is.False);
                var toolbar = window.rootVisualElement.WorldToLocal(canvas.LocalToWorld(new Vector2(rect.center.x, 5)));
                Assert.That(target.HitTest(toolbar), Is.False);
                var translation = (Vector2)EditorMember.Get(area, "translation");
                var scale = (Vector2)EditorMember.Get(area, "scale");
                long frame = window.selectedFrameIndex;
                var selection = EditorMember.Get(module, "selection");
                var screen = window.position.position + point;
                var settings = new TrackpadPreferences();
                target.Apply(new TrackpadEvent
                {
                    Kind = GestureKind.Magnify, Magnification = 0.2, ScreenX = screen.x, ScreenY = screen.y
                }, settings);
                var zoomedScale = (Vector2)EditorMember.Get(area, "scale");
                var zoomedTranslation = (Vector2)EditorMember.Get(area, "translation");
                float anchor = rect.width * 0.5f;
                Assert.That((anchor - zoomedTranslation.x) / zoomedScale.x, Is.EqualTo((anchor - translation.x) / scale.x).Within(0.001f));
                Assert.That(zoomedScale.x, Is.GreaterThan(scale.x));
                Assert.That(zoomedScale.y, Is.EqualTo(scale.y));
                Assert.That(zoomedTranslation.y, Is.EqualTo(translation.y).Within(0.001f));
                target.Apply(new TrackpadEvent
                {
                    Kind = GestureKind.Scroll, DeltaX = 2.75, DeltaY = -20
                }, settings);
                var panned = (Vector2)EditorMember.Get(area, "translation");
                Assert.That(panned.x, Is.EqualTo(zoomedTranslation.x + 2.75f).Within(0.001f));
                Assert.That(panned.y, Is.LessThan(zoomedTranslation.y));
                Assert.That(window.selectedFrameIndex, Is.EqualTo(frame));
                Assert.That(EditorMember.Get(module, "selection"), Is.SameAs(selection));
                viewType.SetValue(module, Enum.Parse(viewType.PropertyType, "Hierarchy"));
                Assert.That(target.HitTest(point), Is.False);
                Assert.That(NavigationTargets.Resolve(window, point, settings), Is.Null);
                target.Apply(new TrackpadEvent
                {
                    Kind = GestureKind.Scroll, DeltaX = 100
                }, settings);
                Assert.That((Vector2)EditorMember.Get(area, "translation"), Is.EqualTo(panned));
            }
            finally
            {
                record.Invoke(window, new object[] { originalRecording });
                ProfilerDriver.profileEditor = originalProfileEditor;
                window.Close();
            }
        }
    }
}
