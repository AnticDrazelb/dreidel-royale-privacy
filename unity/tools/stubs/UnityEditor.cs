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

        public static void SetApplicationIdentifier(BuildTargetGroup g, string id) {}
        public static void SetScriptingBackend(BuildTargetGroup g, ScriptingImplementation i) {}
        public static ScriptingImplementation GetScriptingBackend(BuildTargetGroup g) { return ScriptingImplementation.IL2CPP; }
        public static void SetManagedStrippingLevel(BuildTargetGroup g, ManagedStrippingLevel l) {}
        public static void SetApiCompatibilityLevel(BuildTargetGroup g, ApiCompatibilityLevel l) {}
        public static void SetUseDefaultGraphicsAPIs(BuildTarget t, bool v) {}
        public static void SetGraphicsAPIs(BuildTarget t, GraphicsDeviceType[] apis) {}
        public static GraphicsDeviceType[] GetGraphicsAPIs(BuildTarget t) { return new GraphicsDeviceType[0]; }
        public static void SetArchitecture(BuildTargetGroup g, int arch) {}

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

    public static class AssetDatabase { public static void SaveAssets() {} }

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
