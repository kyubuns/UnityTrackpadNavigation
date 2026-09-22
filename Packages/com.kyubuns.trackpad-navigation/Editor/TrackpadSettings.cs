using System;
using UnityEditor;
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
        public bool Momentum;
        public bool SceneIntegration = true;
        public bool GraphIntegration = true;

        public void Validate()
        {
            PanSensitivity = NavigationMath.Sensitivity(PanSensitivity);
            ZoomSensitivity = NavigationMath.Sensitivity(ZoomSensitivity);
            OrbitSensitivity = NavigationMath.Sensitivity(OrbitSensitivity);
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
        }
        public static void Reset()
        {
            Current = new TrackpadPreferences();
            Save();
        }
    }
}
