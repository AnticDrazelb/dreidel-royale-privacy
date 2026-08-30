using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
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
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
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
            EnableBothInputBackends();
            EnsureShadersAreIncluded(log);
            Validate();
        }

        static void ConfigureAndroid(List<string> log)
        {
            // ARCore requires API 24, and Play requires 64-bit, which means IL2CPP.
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            log.Add("android: minSdk 24, IL2CPP, ARMv7+ARM64");

            // ARCore could not run under Vulkan until AR Foundation 5.1, which is why older
            // guides tell you to pin OpenGLES3. On this line Vulkan works and is meaningfully
            // faster, so it leads and GLES3 follows as the fallback for phones that lack it.
            // The list is explicit rather than automatic because the automatic list has
            // dropped GLES3 before, and a device with no fallback simply fails to start.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            log.Add("android: graphics APIs Vulkan then OpenGLES3");

            // Sockets and reflection-lite serialisation: keep stripping conservative.
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard_2_0);
            log.Add("android: stripping Low, .NET Standard 2.0");
        }

        static void ConfigureIos(List<string> log)
        {
            // ARKit needs 11; 12 is the floor the current toolchain builds against anyway.
            PlayerSettings.iOS.targetOSVersionString = "12.0";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(NamedBuildTarget.iOS, 1);   // ARM64
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
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Low);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.iOS, ApiCompatibilityLevel.NET_Standard_2_0);
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
        static bool InputHandlingIsBoth()
        {
            try
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (assets == null || assets.Length == 0) return true;   // can't tell; don't cry wolf
                var prop = new SerializedObject(assets[0]).FindProperty("activeInputHandler");
                return prop == null || prop.intValue == 2;
            }
            catch { return true; }
        }

        /// <summary>
        /// PlayerSettings.cloudProjectId has moved between Unity versions and is obsolete in
        /// some, so it is read reflectively: a missing property should read as "can't tell",
        /// not stop the whole check from compiling.
        /// </summary>
        static string CloudProjectId()
        {
            try
            {
                var prop = typeof(PlayerSettings).GetProperty("cloudProjectId",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop == null) return "unknown";
                return prop.GetValue(null, null) as string;
            }
            catch { return "unknown"; }
        }

        /// <summary>
        /// AR Foundation 6 tracks the camera with the Input System's TrackedPoseDriver, but
        /// every gesture, button and menu in this game reads the legacy Input class. Only
        /// "Both" satisfies each of them; letting the Input System's install prompt switch the
        /// project to New-only silently kills all touch input, which presents as a game that
        /// builds, runs, and ignores you.
        ///
        /// There is no PlayerSettings API for it, so the setting is written through the
        /// serialised object. Unity reloads the domain afterwards.
        /// </summary>
        static void EnableBothInputBackends()
        {
            try
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (assets == null || assets.Length == 0) return;
                var so = new SerializedObject(assets[0]);
                var prop = so.FindProperty("activeInputHandler");
                if (prop == null) return;
                if (prop.intValue == 2) return;      // already Both
                prop.intValue = 2;                   // 0 = Old, 1 = New, 2 = Both
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                Debug.Log("[Dreidel Royale] Active Input Handling set to Both. Unity will reload.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Dreidel Royale] Couldn't set Active Input Handling: " + e.Message
                                 + "\nSet it to Both by hand under Project Settings > Player.");
            }
        }

        /// <summary>
        /// Every shader this game uses, resolved by name at runtime.
        ///
        /// That is a direct consequence of generating the whole scene in code: no material
        /// in any asset references any of these, so as far as the build pipeline can tell
        /// nothing needs them, and it strips them. The editor is unaffected — it has the
        /// whole shader library loaded — so the failure appears only in a player build, as
        /// a magenta table on a phone after everything looked right on the desk.
        ///
        /// Naming them here is what keeps them in the build.
        /// </summary>
        static readonly string[] RuntimeShaders =
        {
            "Standard",
            "Unlit/Texture",
            "Unlit/Color",
            "Unlit/Transparent",
            "Sprites/Default",
            "DreidelRoyale/ShadowCatcher",
            "DreidelRoyale/AcesToneMap",

            // MatUtil.Additive walks this list in order, and it is every halo, aura, ring
            // and flame in the game. Missing it the first time round meant the fix looked
            // complete while the candles, the gem glow and the win burst would all still
            // have fallen back to Sprites/Default on device.
            "Legacy Shaders/Particles/Additive",
            "Mobile/Particles/Additive",
            "Particles/Standard Unlit",
        };

        static void EnsureShadersAreIncluded(List<string> log)
        {
            try
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
                if (assets == null || assets.Length == 0) return;
                var so = new SerializedObject(assets[0]);
                var list = so.FindProperty("m_AlwaysIncludedShaders");
                if (list == null) return;

                var present = new HashSet<string>();
                for (int i = 0; i < list.arraySize; i++)
                {
                    var sh = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                    if (sh != null) present.Add(sh.name);
                }

                int added = 0;
                foreach (var name in RuntimeShaders)
                {
                    if (present.Contains(name)) continue;
                    var shader = Shader.Find(name);
                    if (shader == null)
                    {
                        log.Add("shaders: '" + name + "' not found in this project - skipped");
                        continue;
                    }
                    list.InsertArrayElementAtIndex(list.arraySize);
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                    added++;
                }
                if (added > 0)
                {
                    so.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                }
                log.Add("shaders: " + added + " added to Always Included ("
                        + RuntimeShaders.Length + " needed at runtime)");
            }
            catch (Exception e)
            {
                log.Add("shaders: could not update Always Included - " + e.Message);
            }
        }

        /// <summary>Which runtime shaders are still missing from the build, if any.</summary>
        static List<string> MissingIncludedShaders()
        {
            var missing = new List<string>();
            try
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
                if (assets == null || assets.Length == 0) return missing;
                var list = new SerializedObject(assets[0]).FindProperty("m_AlwaysIncludedShaders");
                if (list == null) return missing;

                var present = new HashSet<string>();
                for (int i = 0; i < list.arraySize; i++)
                {
                    var sh = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                    if (sh != null) present.Add(sh.name);
                }
                foreach (var name in RuntimeShaders)
                    if (!present.Contains(name) && Shader.Find(name) != null) missing.Add(name);
            }
            catch { }
            return missing;
        }

        [MenuItem("Dreidel Royale/Validate build settings")]
        public static void Validate()
        {
            var problems = new List<string>();

            if (PlayerSettings.colorSpace != ColorSpace.Linear)
                problems.Add("Colour space is Gamma. The gems and emissives are authored linear and will look flat.");

            var androidApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (!androidApis.Contains(GraphicsDeviceType.OpenGLES3))
                problems.Add("Android has no OpenGLES3 fallback. Phones without Vulkan will not start.");

            if ((int)PlayerSettings.Android.minSdkVersion < 24)
                problems.Add("Android minSdkVersion is below 24, which ARCore requires.");

            if (string.IsNullOrEmpty(PlayerSettings.iOS.cameraUsageDescription))
                problems.Add("iOS camera usage description is empty. iOS terminates the app when it touches the camera.");

            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
                problems.Add("Android is on Mono. Play requires a 64-bit binary, which needs IL2CPP.");

            // Relay reads the project id out of the build. Without a linked project, Online
            // fails at the first step on a real phone and nowhere else - which is exactly the
            // kind of thing that gets discovered after a store submission rather than before.
            var missingShaders = MissingIncludedShaders();
            if (missingShaders.Count > 0)
                problems.Add("These shaders are resolved by name at runtime but are not in Always "
                             + "Included Shaders, so the build will strip them and the game will render "
                             + "magenta on device: " + string.Join(", ", missingShaders.ToArray()));

            if (!InputHandlingIsBoth())
                problems.Add("Active Input Handling is not \"Both\". AR camera tracking needs the "
                             + "Input System and every control in this game reads the legacy one.");

            if (string.IsNullOrEmpty(CloudProjectId()))
                problems.Add("No Unity project is linked, so Online play will fail. "
                             + "Link one under Edit > Project Settings > Services (it is free). "
                             + "Same Wi-Fi play works without it.");

            if (problems.Count == 0)
                Debug.Log("[Dreidel Royale] Build settings look right for Android and iOS.");
            else
                Debug.LogWarning("[Dreidel Royale] Build settings need attention:\n  - "
                                 + string.Join("\n  - ", problems.ToArray()));
        }
    }
}
