using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DreidelRoyale.EditorTools
{
    /// <summary>
    /// Configures the project for Android and iOS, and reports on anything it can't fix.
    ///
    /// This exists as a script rather than a committed ProjectSettings.asset because that file
    /// is enormous, version-sensitive, and silently rewritten by the editor. A script says
    /// exactly what it changed and why, survives a Unity upgrade, and can be re-run after
    /// someone flips a setting by hand.
    ///
    /// Menu: Dreidel Royale -> Configure for Android and iOS
    /// </summary>
    public static class PlatformSetup
    {
        public const string BundleId = "com.anticdrazelb.dreidelroyale";

        [MenuItem("Dreidel Royale/Configure for Android and iOS")]
        public static void Configure()
        {
            var log = new List<string>();

            // ---- identity ----
            PlayerSettings.companyName = "Antic Drazelb";
            PlayerSettings.productName = "Dreidel Royale";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, BundleId);
            log.Add("identity: " + BundleId);

            // ---- presentation ----
            // The whole UI is laid out portrait (the canvas reference is 420x860), and a
            // dreidel on a table is a portrait subject.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            log.Add("orientation: portrait only");

            // The art is authored as a linear PBR workflow - the web build renders sRGB with
            // ACES tone mapping - so gamma space would wash out every gem and emissive.
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                log.Add("colour space: Linear (was Gamma) - this triggers a full reimport");
            }

            ConfigureAndroid(log);
            ConfigureIos(log);
            EnableXrLoaders(log);

            AssetDatabase.SaveAssets();
            Debug.Log("[Dreidel Royale] Platform setup:\n  - " + string.Join("\n  - ", log.ToArray()));
            Validate();
        }

        static void ConfigureAndroid(List<string> log)
        {
            // ARCore requires API 24, and Play requires 64-bit, which means IL2CPP.
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            log.Add("android: minSdk 24, IL2CPP, ARMv7+ARM64");

            // ARCore does not run under Vulkan on this AR Foundation line, and an auto graphics
            // API list will happily pick Vulkan first on a modern phone - at which point AR
            // simply never starts, with nothing in the log to say why.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            log.Add("android: graphics API pinned to OpenGLES3 (ARCore will not start under Vulkan)");

            // Sockets and reflection-lite serialisation: keep stripping conservative.
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Android, ApiCompatibilityLevel.NET_Standard_2_0);
            log.Add("android: stripping Low, .NET Standard 2.0");
        }

        static void ConfigureIos(List<string> log)
        {
            // ARKit needs 11; 12 is the floor the current toolchain builds against anyway.
            PlayerSettings.iOS.targetOSVersionString = "12.0";
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(BuildTargetGroup.iOS, 1);   // ARM64
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
            log.Add("ios: target 12.0, IL2CPP, ARM64, Metal");

            // Reaching the camera without a usage description does not warn - it terminates
            // the app. The plist post-process sets these too; setting them here means they are
            // also right for anyone building straight from the editor.
            PlayerSettings.iOS.cameraUsageDescription =
                "Dreidel Royale uses the camera to place the dreidel board on a real table.";
            log.Add("ios: camera usage description set");

            // AR is a feature, not a requirement: the app must still install and play on a
            // device with no ARKit.
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.iOS, ManagedStrippingLevel.Low);
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.iOS, ApiCompatibilityLevel.NET_Standard_2_0);
            log.Add("ios: stripping Low, .NET Standard 2.0");
        }

        /// <summary>
        /// Ticking ARCore and ARKit in XR Plug-in Management is the step everyone forgets, and
        /// its failure mode is the worst kind: the app builds, runs, and reports that AR is
        /// unsupported on a phone that plainly supports it.
        ///
        /// Reached by reflection on purpose. The XR Management editor API has moved between
        /// versions, and a signature change here would be a compile error that stops the whole
        /// project building; a reflection miss is a warning telling you which two boxes to tick.
        /// </summary>
        static void EnableXrLoaders(List<string> log)
        {
            if (!TryAssignLoader(BuildTargetGroup.Android, "UnityEngine.XR.ARCore.ARCoreLoader"))
                log.Add("XR: could not tick ARCore automatically - do it in Project Settings > XR Plug-in Management > Android");
            else log.Add("XR: ARCore loader enabled for Android");

            if (!TryAssignLoader(BuildTargetGroup.iOS, "UnityEngine.XR.ARKit.ARKitLoader"))
                log.Add("XR: could not tick ARKit automatically - do it in Project Settings > XR Plug-in Management > iOS");
            else log.Add("XR: ARKit loader enabled for iOS");
        }

        static bool TryAssignLoader(BuildTargetGroup group, string loaderTypeName)
        {
            try
            {
                var store = FindType("UnityEditor.XR.Management.Metadata.XRPackageMetadataStore");
                var settingsType = FindType("UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget");
                if (store == null || settingsType == null) return false;

                // Make sure a settings object exists for this target before assigning into it.
                var ensure = settingsType.GetMethod("GetOrCreate",
                    BindingFlags.Public | BindingFlags.Static);
                if (ensure != null) ensure.Invoke(null, null);

                var assign = store.GetMethod("AssignLoader",
                    BindingFlags.Public | BindingFlags.Static);
                if (assign == null) return false;

                var managerType = FindType("UnityEngine.XR.Management.XRManagerSettings");
                var settings = GetXrManager(settingsType, managerType, group);
                if (settings == null) return false;

                var ok = assign.Invoke(null, new object[] { settings, loaderTypeName, group });
                return ok is bool && (bool)ok;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Dreidel Royale] XR loader assignment skipped: " + e.Message);
                return false;
            }
        }

        static object GetXrManager(Type perTargetType, Type managerType, BuildTargetGroup group)
        {
            var xrGeneralType = FindType("UnityEngine.XR.Management.XRGeneralSettings");
            var editorType = FindType("UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget");
            if (editorType == null) return null;

            EditorBuildSettings.TryGetConfigObject(
                (string)xrGeneralType.GetField("k_SettingsKey", BindingFlags.Public | BindingFlags.Static)
                    .GetValue(null), out UnityEngine.Object obj);
            if (obj == null) return null;

            var get = editorType.GetMethod("SettingsForBuildTarget", BindingFlags.Public | BindingFlags.Instance);
            var general = get != null ? get.Invoke(obj, new object[] { group }) : null;
            if (general == null) return null;

            var mgrProp = general.GetType().GetProperty("Manager", BindingFlags.Public | BindingFlags.Instance);
            return mgrProp != null ? mgrProp.GetValue(general, null) : null;
        }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        // ---------------------------------------------------------------
        /// <summary>
        /// Reports the settings whose failure mode is silence — a build that runs perfectly and
        /// just quietly has no AR, or finds no tables on the network.
        /// </summary>
        [MenuItem("Dreidel Royale/Validate build settings")]
        public static void Validate()
        {
            var problems = new List<string>();

            if (PlayerSettings.colorSpace != ColorSpace.Linear)
                problems.Add("Colour space is Gamma. The gems and emissives are authored linear and will look flat.");

            var androidApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (androidApis.Contains(GraphicsDeviceType.Vulkan))
                problems.Add("Android graphics APIs include Vulkan. ARCore will not start; AR reports 'unsupported' on capable phones.");
            if (!androidApis.Contains(GraphicsDeviceType.OpenGLES3))
                problems.Add("Android is missing OpenGLES3, which ARCore needs.");

            if ((int)PlayerSettings.Android.minSdkVersion < 24)
                problems.Add("Android minSdkVersion is below 24, which ARCore requires.");

            if (string.IsNullOrEmpty(PlayerSettings.iOS.cameraUsageDescription))
                problems.Add("iOS camera usage description is empty. iOS terminates the app when it touches the camera.");

            if (PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) != ScriptingImplementation.IL2CPP)
                problems.Add("Android is on Mono. Play requires a 64-bit binary, which needs IL2CPP.");

            if (problems.Count == 0)
                Debug.Log("[Dreidel Royale] Build settings look right for Android and iOS.");
            else
                Debug.LogWarning("[Dreidel Royale] Build settings need attention:\n  - "
                                 + string.Join("\n  - ", problems.ToArray()));
        }
    }
}
