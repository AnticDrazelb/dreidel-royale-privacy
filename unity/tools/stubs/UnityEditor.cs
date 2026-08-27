// Minimal UnityEditor surface, so the editor scripts type-check outside the editor too.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using GraphicsDeviceType = UnityEngine.Rendering.GraphicsDeviceType;
using ColorSpace = UnityEngine.ColorSpace;

namespace UnityEditor
{
    public enum BuildTarget { Android, iOS, StandaloneWindows64, StandaloneOSX }
    public enum BuildTargetGroup { Unknown, Standalone, Android, iOS }
    public enum ScriptingImplementation { Mono2x, IL2CPP }
    public enum ManagedStrippingLevel { Disabled, Low, Medium, High }
    public enum ApiCompatibilityLevel { NET_Standard_2_0, NET_4_6 }
    public enum AndroidSdkVersions { AndroidApiLevelAuto = 0, AndroidApiLevel24 = 24 }
    [Flags] public enum AndroidArchitecture { None = 0, ARMv7 = 1, ARM64 = 2 }
    public enum UIOrientation { Portrait, PortraitUpsideDown, LandscapeLeft, LandscapeRight, AutoRotation }

    public static class PlayerSettings
    {
        public static string companyName, productName;
        public static ColorSpace colorSpace;
        public static UIOrientation defaultInterfaceOrientation;
        public static bool allowedAutorotateToPortrait, allowedAutorotateToPortraitUpsideDown,
                           allowedAutorotateToLandscapeLeft, allowedAutorotateToLandscapeRight;

        public static void SetApplicationIdentifier(Build.NamedBuildTarget g, string id) {}
        public static void SetScriptingBackend(Build.NamedBuildTarget g, ScriptingImplementation i) {}
        public static ScriptingImplementation GetScriptingBackend(Build.NamedBuildTarget g) { return ScriptingImplementation.IL2CPP; }
        public static void SetManagedStrippingLevel(Build.NamedBuildTarget g, ManagedStrippingLevel l) {}
        public static void SetApiCompatibilityLevel(Build.NamedBuildTarget g, ApiCompatibilityLevel l) {}
        public static void SetUseDefaultGraphicsAPIs(BuildTarget t, bool v) {}
        public static void SetGraphicsAPIs(BuildTarget t, GraphicsDeviceType[] apis) {}
        public static GraphicsDeviceType[] GetGraphicsAPIs(BuildTarget t) { return new GraphicsDeviceType[0]; }
        public static void SetArchitecture(Build.NamedBuildTarget g, int arch) {}

        public static class Android
        {
            public static AndroidSdkVersions minSdkVersion, targetSdkVersion;
            public static AndroidArchitecture targetArchitectures;
        }

        public static class iOS
        {
            public static string targetOSVersionString, cameraUsageDescription;
            public static bool requiresFullScreen;
        }
    }

    public class SerializedProperty { public int intValue { get; set; } public bool boolValue { get; set; } }
    public class SerializedObject {
        public SerializedObject(UnityEngine.Object o){}
        public SerializedProperty FindProperty(string path){return null;}
        public bool ApplyModifiedProperties(){return false;}
    }
    public static class AssetDatabase {
        public static UnityEngine.Object[] LoadAllAssetsAtPath(string path){return null;} public static void SaveAssets() {} }

    public static class EditorBuildSettings
    {
        public static bool TryGetConfigObject(string key, out UnityEngine.Object obj) { obj = null; return false; }
    }

    public class MenuItem : Attribute { public MenuItem(string path) {} public MenuItem(string path, bool v, int p) {} }
}

namespace UnityEditor.Callbacks
{
    public class PostProcessBuildAttribute : Attribute
    {
        public PostProcessBuildAttribute() {}
        public PostProcessBuildAttribute(int order) {}
    }
}

namespace UnityEditor.Build
{
    public interface IOrderedCallback { int callbackOrder { get; } }
    public interface IPostGenerateGradleAndroidProject : IOrderedCallback
    {
        void OnPostGenerateGradleAndroidProject(string path);
    }
}

namespace UnityEditor.Build.Reporting
{
    public class BuildReport {}
}

namespace UnityEditor.Build
{
    /// Replaced the BuildTargetGroup overloads of PlayerSettings in Unity 6.
    public struct NamedBuildTarget
    {
        public static NamedBuildTarget Android { get { return default(NamedBuildTarget); } }
        public static NamedBuildTarget iOS { get { return default(NamedBuildTarget); } }
        public static NamedBuildTarget Standalone { get { return default(NamedBuildTarget); } }
    }
}
