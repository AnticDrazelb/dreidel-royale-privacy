using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace DreidelRoyale.EditorTools
{
    /// <summary>
    /// The permissions both stores need, written into the generated project at build time.
    ///
    /// These are done as post-processing rather than as checked-in manifest files because a
    /// hand-written AndroidManifest.xml REPLACES Unity's, taking the launcher activity with
    /// it, and because the AR packages contribute manifest entries of their own that a
    /// replacement would discard.
    /// </summary>
    public static class BuildPostProcess
    {
#if UNITY_IOS
        [PostProcessBuild(999)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;

            var plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;

            // Touching the camera without this does not warn - iOS terminates the app.
            root.SetString("NSCameraUsageDescription",
                "Dreidel Royale uses the camera to place the dreidel board on a real table.");

            // iOS 14 gates the local network behind a prompt, and without this key the prompt
            // never appears - the connection simply finds nothing, with no error to explain it.
            // This is what makes finding a friend's table work at all on an iPhone.
            root.SetString("NSLocalNetworkUsageDescription",
                "Dreidel Royale finds other players' tables on your Wi-Fi so you can play together.");

            // Declared for completeness: the discovery path deliberately avoids Bonjour and
            // raw broadcast, because broadcast and multicast need an entitlement Apple grants
            // only on request. Plain outbound TCP needs none of that.
            var bonjour = root.CreateArray("NSBonjourServices");
            bonjour.AddString("_dreidelroyale._tcp");

            // The board is a portrait subject and the whole UI is laid out for it.
            var orientations = root.CreateArray("UISupportedInterfaceOrientations");
            orientations.AddString("UIInterfaceOrientationPortrait");

            plist.WriteToFile(plistPath);
            Debug.Log("[Dreidel Royale] Info.plist: camera + local network usage descriptions written.");
        }
#endif
    }

    /// <summary>
    /// Adds the two Wi-Fi permissions the LAN transport needs. Everything else - camera, the
    /// ARCore metadata - the AR packages already contribute, so this only merges in what is
    /// genuinely ours.
    /// </summary>
    public class AndroidManifestPatch : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder { get { return 1; } }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning("[Dreidel Royale] No AndroidManifest at " + manifestPath);
                return;
            }

            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var manifest = doc.DocumentElement;
            if (manifest == null) return;
            const string ns = "http://schemas.android.com/apk/res/android";

            // CHANGE_WIFI_MULTICAST_STATE is the one that matters: without the multicast lock
            // it allows, Android's Wi-Fi driver drops the discovery broadcast before the app
            // ever sees it, and a host on the same network is simply never found.
            AddPermission(doc, manifest, ns, "android.permission.INTERNET");
            AddPermission(doc, manifest, ns, "android.permission.ACCESS_WIFI_STATE");
            AddPermission(doc, manifest, ns, "android.permission.CHANGE_WIFI_MULTICAST_STATE");

            doc.Save(manifestPath);
            Debug.Log("[Dreidel Royale] AndroidManifest: Wi-Fi discovery permissions merged.");
        }

        static void AddPermission(XmlDocument doc, XmlElement manifest, string ns, string name)
        {
            foreach (XmlNode node in manifest.SelectNodes("uses-permission"))
            {
                var attr = node.Attributes == null ? null : node.Attributes["name", ns];
                if (attr != null && attr.Value == name) return;      // already there
            }
            var el = doc.CreateElement("uses-permission");
            el.SetAttribute("name", ns, name);
            manifest.AppendChild(el);
        }
    }
}
