using System;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace TrackpadNavigation
{
    [Serializable]
    internal sealed class TrackpadPreferences
    {
        public bool Enabled = true;
        public float PanSensitivity = 1;
        public float ZoomSensitivity = 1;
        public float OrbitSensitivity = 1;
        public bool InvertNaturalScrolling;
        public bool InvertZoom;
        public bool InvertPanX, InvertPanY, InvertOrbitX, InvertOrbitY;
        public bool Momentum = true;
        public bool SceneIntegration = true;
        public bool GraphIntegration = true;
        public float VfxZoomStepSize = ContentZoomer.DefaultScaleStep;
        public float BuilderZoomStepSize = BuilderScrollZoom.DefaultStep;

        public void Validate()
        {
            PanSensitivity = NavigationMath.Sensitivity(PanSensitivity);
            ZoomSensitivity = NavigationMath.Sensitivity(ZoomSensitivity);
            OrbitSensitivity = NavigationMath.Sensitivity(OrbitSensitivity);
            VfxZoomStepSize = float.IsNaN(VfxZoomStepSize) || float.IsInfinity(VfxZoomStepSize) ? ContentZoomer.DefaultScaleStep : Mathf.Clamp(VfxZoomStepSize, 0.001f, 1);
            BuilderZoomStepSize = float.IsNaN(BuilderZoomStepSize) || float.IsInfinity(BuilderZoomStepSize) ? BuilderScrollZoom.DefaultStep : Mathf.Clamp(BuilderZoomStepSize, 0.001f, 1);
        }

        public static TrackpadPreferences FromJson(string json)
        {
            var result = new TrackpadPreferences();
            try
            {
                if (!string.IsNullOrWhiteSpace(json))
                {
                    JsonUtility.FromJsonOverwrite(json, result);
                }
            }
            catch (ArgumentException)
            {
                return new TrackpadPreferences();
            }
            result.Validate();
            return result;
        }
    }

    internal static class TrackpadSettings
    {
        const string Key = "com.kyubuns.trackpad-navigation.preferences.v1";
        public static TrackpadPreferences Current {
            get; private set;
        } = TrackpadPreferences.FromJson(EditorPrefs.GetString(Key, ""));
        public static void Save()
        {
            Current.Validate();
            EditorPrefs.SetString(Key, JsonUtility.ToJson(Current));
            TrackpadNavigator.ClearTarget();
            VfxGraphZoom.Refresh();
            BuilderScrollZoom.Refresh();
        }
        public static void Reset()
        {
            Current = new TrackpadPreferences();
            Save();
        }
    }
}
