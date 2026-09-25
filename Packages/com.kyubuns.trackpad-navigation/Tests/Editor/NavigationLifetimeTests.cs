using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TrackpadNavigation.Tests
{
    public sealed class NavigationLifetimeTests
    {
        sealed class TestWindow : EditorWindow
        {
        }

        sealed class TestGraph : GraphView
        {
        }

        [UnityTest]
        public IEnumerator DisposedCanvasReleasesCaptureAndRejectsQueuedInput()
        {
            var window = ScriptableObject.CreateInstance<TestWindow>();
            TrackpadCanvas canvas = null;
            try
            {
                window.Show();
                var viewport = window.rootVisualElement;
                var content = new VisualElement();
                content.style.width = content.style.height = 600;
                viewport.Add(content);
                var button = new Button
                {
                    pickingMode = PickingMode.Ignore
                };
                button.style.position = Position.Absolute;
                button.style.left = button.style.top = 0;
                button.style.width = button.style.height = 150;
                viewport.Add(button);
                int changes = 0;
                canvas = new TrackpadCanvas(viewport, content, new Vector2(0.1f, 10), (_, _) => ++changes);
                yield return null;
                var point = NavigationCoordinates.ToWindow(window, viewport, new Vector2(80, 80));
                var target = canvas.Target(window);
                var screen = window.position.position + point;
                var pointer = new NativePointer
                {
                    X = screen.x, Y = screen.y, Active = 1, WindowNumber = 1
                };
                var settings = new TrackpadPreferences();
                var capture = TrackpadNavigator.CaptureFor(window, pointer, settings);
                Assert.That(capture.Kinds, Is.EqualTo(3));
                Assert.That(target.IsAvailable, Is.True);
                Assert.That(target.HitTest(point), Is.True);

                // 開始後にカーソル下へ操作UIが来ても対象自体は失効しない。
                button.pickingMode = PickingMode.Position;
                var overControl = TrackpadNavigator.CaptureFor(window, pointer, settings);
                Assert.That(overControl.Target, Is.EqualTo(capture.Target));
                Assert.That(overControl.Kinds, Is.Zero);
                Assert.That(target.IsAvailable, Is.True);
                Assert.That(target.HitTest(point), Is.False);

                canvas.Dispose();
                var position = content.style.translate.value;
                int originalChanges = changes;
                Assert.That(target.IsAvailable, Is.False);
                target.Apply(new TrackpadEvent
                {
                    Kind = GestureKind.Scroll, DeltaX = 10, DeltaY = 20
                }, settings);
                Assert.That(content.style.translate.value, Is.EqualTo(position));
                Assert.That(changes, Is.EqualTo(originalChanges));
                var released = TrackpadNavigator.CaptureFor(window, pointer, settings);
                Assert.That(released.Kinds, Is.Zero);
                Assert.That(released.Target, Is.Not.EqualTo(capture.Target));
            }
            finally
            {
                TrackpadNavigator.ClearTarget();
                canvas?.Dispose();
                window.Close();
            }
        }

        [UnityTest]
        public IEnumerator ReparentedGraphCannotBeMovedByItsOldWindow()
        {
            var first = ScriptableObject.CreateInstance<TestWindow>();
            var second = ScriptableObject.CreateInstance<TestWindow>();
            try
            {
                first.Show();
                second.Show();
                var graph = new TestGraph();
                graph.style.flexGrow = 1;
                graph.SetupZoom(0.1f, 5);
                first.rootVisualElement.Add(graph);
                yield return null;
                var target = new GraphNavigation(first, graph);
                Assert.That(target.IsAvailable, Is.True);
                second.rootVisualElement.Add(graph);
                yield return null;
                var before = graph.viewTransform.position;
                Assert.That(graph.panel, Is.Not.Null);
                Assert.That(target.IsAvailable, Is.False);
                target.Apply(new TrackpadEvent
                {
                    Kind = GestureKind.Scroll, DeltaX = 100, DeltaY = 100
                }, new TrackpadPreferences());
                Assert.That(graph.viewTransform.position, Is.EqualTo(before));
                second.Close();
                Assert.That(new GraphNavigation(second, graph).IsAvailable, Is.False);
            }
            finally
            {
                if (first != null)
                {
                    first.Close();
                }
                if (second != null)
                {
                    second.Close();
                }
            }
        }
    }
}
