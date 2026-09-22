using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace TrackpadNavigation.Tests
{
    public sealed class PlatformIsolationTests
    {
        [Serializable]
        sealed class Definition
        {
            public string[] includePlatforms = Array.Empty<string>();
            public string[] defineConstraints = Array.Empty<string>();
        }

        [Test]
        public void AssembliesTargetOnlyMacEditor()
        {
            var names = new[] {
                typeof(TrackpadCanvas).Assembly.GetName().Name,
                GetType().Assembly.GetName().Name
            };
            foreach (var name in names)
            {
                var path = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(name);
                var definition = JsonUtility.FromJson<Definition>(File.ReadAllText(path));
                CollectionAssert.AreEquivalent(new[] {
                    "Editor"
                }, definition.includePlatforms, name);
                CollectionAssert.AreEquivalent(new[] {
                    "UNITY_EDITOR_OSX"
                }, definition.defineConstraints, name);
            }
            Assert.That(CompilationPipeline.GetAssemblies(AssembliesType.Player).Any(assembly => names.Contains(assembly.name)), Is.False);
        }

        [Test]
        public void NativePluginTargetsOnlyMacArmEditor()
        {
            var importer = (PluginImporter)AssetImporter.GetAtPath("Packages/com.kyubuns.trackpad-navigation/Plugins/macOS/TrackpadBridge.bundle");
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.GetCompatibleWithEditor(), Is.True);
            Assert.That(importer.GetEditorData("OS"), Is.EqualTo("OSX"));
            Assert.That(importer.GetEditorData("CPU"), Is.EqualTo("ARM64"));
            Assert.That(importer.GetCompatibleWithAnyPlatform(), Is.False);
            foreach (var target in new[] {
                BuildTarget.StandaloneOSX,
                BuildTarget.StandaloneWindows64,
                BuildTarget.StandaloneLinux64,
                BuildTarget.Android,
                BuildTarget.iOS,
                BuildTarget.WebGL
            })
            {
                Assert.That(importer.GetCompatibleWithPlatform(target), Is.False, target.ToString());
            }
        }
    }
}
