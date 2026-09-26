using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TrackpadNavigation.Tests
{
    public sealed class SpriteNavigationTests
    {
        [UnityTest]
        public IEnumerator SpriteViewPreservesCursorAnchorAndImportData()
        {
            var type = Type.GetType("UnityEditor.U2D.Sprites.SpriteEditorWindow, Unity.2D.Sprite.Editor");
            if (type == null)
            {
                Assert.Ignore("2D Sprite is not installed.");
            }
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/TrackpadSpriteTest.png");
            var previousSelection = Selection.activeObject;
            EditorWindow window = null;
            try
            {
                var generated = new Texture2D(256, 256);
                File.WriteAllBytes(path, generated.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(generated);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                string originalImportData = File.ReadAllText(path + ".meta");
                Selection.activeObject = texture;
                window = (EditorWindow)ScriptableObject.CreateInstance(type);
                window.Show();
                window.position = new Rect(100, 100, 850, 600);
                EditorMember.Set(window, "selectedObject", texture);
                for (int i = 0; i < 10; ++i)
                {
                    window.Repaint();
                    yield return null;
                }
                var canvas = window.rootVisualElement.Q<IMGUIContainer>("mainViewIMGUIElement");
                var rect = (Rect)EditorMember.Get(window, "windowDimension");
                var point = NavigationCoordinates.ToWindow(window, canvas, rect.center);
                var target = NavigationTargets.Resolve(window, point, new TrackpadPreferences());
                Assert.That(target, Is.TypeOf<SpriteNavigation>());
                Assert.That(target.HitTest(point), Is.True);
                Assert.That(target.HitTest(new Vector2(point.x, 5)), Is.False);
                var scrollbar = NavigationCoordinates.ToWindow(window, canvas, new Vector2(rect.xMax + 3, rect.center.y));
                Assert.That(target.HitTest(scrollbar), Is.False);
                var selection = EditorMember.Get(window, "selectedSpriteRect");
                var settings = new TrackpadPreferences();
                var beforeScroll = (Vector2)EditorMember.Get(window, "scrollPosition");
                target.Apply(new TrackpadEvent
                {
                    Kind = GestureKind.Scroll, DeltaX = 3.5, DeltaY = -4.25
                }, settings);
                var scroll = (Vector2)EditorMember.Get(window, "scrollPosition");
                Assert.That(Vector2.Distance(scroll, beforeScroll - new Vector2(3.5f, -4.25f)), Is.LessThan(0.001f));
                float zoom = (float)EditorMember.Get(window, "zoomLevel");
                var anchor = new Vector2(-40, -30);
                point = NavigationCoordinates.ToWindow(window, canvas, rect.center + anchor);
                var screen = window.position.position + point;
                var pinch = new TrackpadEvent
                {
                    Kind = GestureKind.Magnify, Magnification = 0.2, ScreenX = screen.x, ScreenY = screen.y
                };
                target.Apply(pinch, settings);
                float nextZoom = (float)EditorMember.Get(window, "zoomLevel");
                var nextScroll = (Vector2)EditorMember.Get(window, "scrollPosition");
                Assert.That(nextZoom, Is.GreaterThan(zoom));
                Assert.That(Vector2.Distance((anchor + nextScroll) / nextZoom, (anchor + scroll) / zoom), Is.LessThan(0.001f));
                pinch.Magnification = -0.2;
                target.Apply(pinch, settings);
                Assert.That((float)EditorMember.Get(window, "zoomLevel"), Is.EqualTo(zoom).Within(0.001f));
                Assert.That(Vector2.Distance((Vector2)EditorMember.Get(window, "scrollPosition"), scroll), Is.LessThan(0.001f));
                yield return null;
                Assert.That(EditorMember.Get(window, "textureIsDirty"), Is.False);
                Assert.That(EditorMember.Get(window, "selectedSpriteRect"), Is.SameAs(selection));
                Assert.That(File.ReadAllText(path + ".meta"), Is.EqualTo(originalImportData));
                type.GetMethod("RefreshPropertiesCache").Invoke(window, null);
                Assert.That(target.HitTest(point), Is.False);
            }
            finally
            {
                if (window != null)
                {
                    window.Close();
                }
                Selection.activeObject = previousSelection;
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
