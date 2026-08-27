using System;
using UnityEngine;

namespace DreidelRoyale.Core
{
    /// <summary>
    /// The store bridge behind "Unlock Full Collection".
    ///
    /// The billing library itself is native and per-platform, so this is the seam the web
    /// build had in the same place: ask the native side if it is there, and if it isn't, say
    /// so plainly rather than pretending. What this file will not do is hand the entitlement
    /// out for free — a button that grants a paid unlock on tap is not a stub, it is a
    /// giveaway, and it would ship as one.
    ///
    /// The native side calls back into `Bootstrap` (a GameObject named "Bootstrap" carrying
    /// <see cref="IapCallbacks"/>) with `OnPurchaseComplete` / `OnPurchaseRestored`, matching
    /// the two globals the web build exposed to its Android wrapper.
    /// </summary>
    public static class Iap
    {
        /// <summary>Raised on the main thread once an entitlement actually lands.</summary>
        public static event Action<bool> OnEntitled;    // true = celebrate, false = quiet restore

        /// <summary>
        /// Whether a purchase can be started at all. False in the editor and in any build
        /// whose native billing class is absent, which is what makes the honest message
        /// below reachable instead of a silent no-op.
        /// </summary>
        public static bool BridgeAvailable
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return AndroidBilling() != null;
#elif UNITY_IOS && !UNITY_EDITOR
                return _DreidelIapAvailable() != 0;
#else
                return false;
#endif
            }
        }

        /// <summary>Start the purchase flow. Returns false if there is nothing to start.</summary>
        public static bool RequestFullCollection()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                var billing = AndroidBilling();
                if (billing == null) return false;
                billing.Call("buyFullCollection");
                return true;
#elif UNITY_IOS && !UNITY_EDITOR
                if (_DreidelIapAvailable() == 0) return false;
                _DreidelIapBuyFullCollection();
                return true;
#else
                return false;
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Iap] purchase failed to start: " + e.Message);
                return false;
            }
        }

        /// <summary>Ask the store to replay what this account already owns.</summary>
        public static bool RequestRestore()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                var billing = AndroidBilling();
                if (billing == null) return false;
                billing.Call("restorePurchases");
                return true;
#elif UNITY_IOS && !UNITY_EDITOR
                if (_DreidelIapAvailable() == 0) return false;
                _DreidelIapRestore();
                return true;
#else
                return false;
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Iap] restore failed to start: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Called by the native side (or by the debug code) once an entitlement is real.
        /// `celebrate` is false for a restore, which should feel like a correction rather
        /// than a purchase.
        /// </summary>
        public static void GrantEntitlement(bool celebrate)
        {
            bool wasNew = !Unlocks.OwnsFullCollection();
            Unlocks.GrantFullCollection();
            if (OnEntitled != null) OnEntitled(celebrate && wasNew);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// The native wrapper is optional, so it is looked up by name and a miss is a normal
        /// outcome, not an error: a build without a billing class simply has no store.
        /// </summary>
        static AndroidJavaObject AndroidBilling()
        {
            if (_androidChecked) return _androidBilling;
            _androidChecked = true;
            try
            {
                using (var cls = new AndroidJavaClass("com.dreidelroyale.Billing"))
                    _androidBilling = cls.CallStatic<AndroidJavaObject>("instance");
            }
            catch { _androidBilling = null; }
            return _androidBilling;
        }

        static AndroidJavaObject _androidBilling;
        static bool _androidChecked;
#endif

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern int _DreidelIapAvailable();
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void _DreidelIapBuyFullCollection();
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void _DreidelIapRestore();
#endif
    }

    /// <summary>
    /// The native side's landing pad. It lives on the Bootstrap object so a native
    /// `UnitySendMessage("Bootstrap", "OnPurchaseComplete", "")` reaches it, which is the
    /// same shape as the two window globals the web build published.
    /// </summary>
    public class IapCallbacks : MonoBehaviour
    {
        public void OnPurchaseComplete(string _) { Iap.GrantEntitlement(true); }
        public void OnPurchaseRestored(string _) { Iap.GrantEntitlement(false); }
    }
}
