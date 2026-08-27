// Minimal Unity API surface, for type-checking the port outside the editor.
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector2 {
        public float x, y;
        public Vector2(float x, float y){this.x=x;this.y=y;}
        public float magnitude { get { return 0; } }
        public float sqrMagnitude { get { return 0; } }
        public Vector2 normalized { get { return this; } }
        public static Vector2 zero, one, up, down, left, right;
        public static Vector2 operator+(Vector2 a, Vector2 b){return a;}
        public static Vector2 operator-(Vector2 a, Vector2 b){return a;}
        public static Vector2 operator-(Vector2 a){return a;}
        public static Vector2 operator*(Vector2 a, float b){return a;}
        public static Vector2 operator*(float b, Vector2 a){return a;}
        public static Vector2 operator/(Vector2 a, float b){return a;}
        public static float Dot(Vector2 a, Vector2 b){return 0;}
        public static float Distance(Vector2 a, Vector2 b){return 0;}
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t){return a;}
        public static implicit operator Vector3(Vector2 v){return new Vector3(v.x,v.y,0);}
    }
    public struct Vector2Int {
        public int x, y;
        public Vector2Int(int x, int y){this.x=x;this.y=y;}
    }
    public struct Vector3 {
        public float x, y, z;
        public Vector3(float x, float y, float z){this.x=x;this.y=y;this.z=z;}
        public Vector3(float x, float y){this.x=x;this.y=y;this.z=0;}
        public float magnitude { get { return 0; } }
        public float sqrMagnitude { get { return 0; } }
        public Vector3 normalized { get { return this; } }
        public void Normalize(){}
        public void Set(float a,float b,float c){}
        public static Vector3 zero, one, up, down, left, right, forward, back;
        public static Vector3 operator+(Vector3 a, Vector3 b){return a;}
        public static Vector3 operator-(Vector3 a, Vector3 b){return a;}
        public static Vector3 operator-(Vector3 a){return a;}
        public static Vector3 operator*(Vector3 a, float b){return a;}
        public static Vector3 operator*(float b, Vector3 a){return a;}
        public static Vector3 operator/(Vector3 a, float b){return a;}
        public static float Dot(Vector3 a, Vector3 b){return 0;}
        public static Vector3 Cross(Vector3 a, Vector3 b){return a;}
        public static float Distance(Vector3 a, Vector3 b){return 0;}
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t){return a;}
        public static Vector3 ClampMagnitude(Vector3 v, float m){return v;}
        public static implicit operator Vector2(Vector3 v){return new Vector2(v.x,v.y);}
    }
    public struct Vector4 {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w){this.x=x;this.y=y;this.z=z;this.w=w;}
    }
    public struct Quaternion {
        public float x, y, z, w;
        public static Quaternion identity;
        public static Quaternion Euler(float x, float y, float z){return identity;}
        public static Quaternion Euler(Vector3 v){return identity;}
        public static Quaternion AngleAxis(float a, Vector3 axis){return identity;}
        public static Quaternion Inverse(Quaternion q){return q;}
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t){return a;}
        public static Quaternion LookRotation(Vector3 f){return identity;}
        public static Quaternion LookRotation(Vector3 f, Vector3 u){return identity;}
        public static Quaternion FromToRotation(Vector3 a, Vector3 b){return identity;}
        public static Quaternion operator*(Quaternion a, Quaternion b){return a;}
        public static Vector3 operator*(Quaternion a, Vector3 b){return b;}
    }
    public struct Color {
        public float r, g, b, a;
        public Color(float r, float g, float b){this.r=r;this.g=g;this.b=b;this.a=1;}
        public Color(float r, float g, float b, float a){this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Color white, black, clear, red, green, blue, magenta;
        public static Color Lerp(Color a, Color b, float t){return a;}
        public static Color HSVToRGB(float h,float s,float v){return white;}
        public static void RGBToHSV(Color c, out float h, out float s, out float v){h=s=v=0;}
        public static Color operator*(Color a, float b){return a;}
        public static Color operator*(Color a, Color b){return a;}
        public static Color operator+(Color a, Color b){return a;}
    }
    public struct Color32 {
        public byte r,g,b,a;
        public Color32(byte r, byte g, byte b, byte a){this.r=r;this.g=g;this.b=b;this.a=a;}
        public static implicit operator Color(Color32 c){return Color.white;}
        public static implicit operator Color32(Color c){return new Color32(0,0,0,0);}
    }
    public static class ColorUtility {
        public static bool TryParseHtmlString(string s, out Color c){c=Color.white;return true;}
        public static string ToHtmlStringRGB(Color c){return "";}
    }
    public struct Rect {
        public float x,y,width,height;
        public Rect(float x,float y,float w,float h){this.x=x;this.y=y;this.width=w;this.height=h;}
        public Vector2 center { get { return Vector2.zero; } }
    }
    public struct Bounds {
        public Vector3 center, size, extents;
        public Bounds(Vector3 c, Vector3 s){center=c;size=s;extents=s;}
    }
    public static class Mathf {
        public const float PI = 3.14159265f;
        public const float Deg2Rad = 0.0174533f;
        public const float Rad2Deg = 57.29578f;
        public const float Infinity = float.PositiveInfinity;
        public static float Abs(float f){return 0;} public static int Abs(int f){return 0;}
        public static float Sin(float f){return 0;} public static float Cos(float f){return 0;}
        public static float Tan(float f){return 0;} public static float Atan2(float a,float b){return 0;}
        public static float Sqrt(float f){return 0;} public static float Pow(float a,float b){return 0;}
        public static float Log(float f){return 0;} public static float Exp(float f){return 0;}
        public static float Min(float a,float b){return 0;} public static int Min(int a,int b){return 0;}
        public static float Max(float a,float b){return 0;} public static int Max(int a,int b){return 0;}
        public static float Clamp(float v,float a,float b){return 0;} public static int Clamp(int v,int a,int b){return 0;}
        public static float Clamp01(float v){return 0;}
        public static float Lerp(float a,float b,float t){return 0;}
        public static float Repeat(float t,float l){return 0;}
        public static float Round(float f){return 0;}
        public static int RoundToInt(float f){return 0;}
        public static int FloorToInt(float f){return 0;}
        public static int CeilToInt(float f){return 0;}
        public static float Floor(float f){return 0;} public static float Ceil(float f){return 0;}
        public static float DeltaAngle(float a,float b){return 0;}
        public static float Sign(float f){return 0;}
        public static float SmoothStep(float a,float b,float t){return 0;}
    }
    public class Object {
        public string name; public HideFlags hideFlags;
        public int GetInstanceID(){return 0;}
        public static void Destroy(Object o){} public static void Destroy(Object o, float t){}
        public static void DestroyImmediate(Object o){}
        public static implicit operator bool(Object o){return true;}
        public static bool operator==(Object a, Object b){return false;}
        public static bool operator!=(Object a, Object b){return true;}
        public override bool Equals(object o){return false;} public override int GetHashCode(){return 0;}
    }
    [Flags] public enum HideFlags { None=0, HideAndDontSave=61 }
    public class Component : Object {
        public GameObject gameObject; public Transform transform;
        public T GetComponent<T>(){return default(T);}
        public T GetComponentInParent<T>(){return default(T);}
        public T GetComponentInChildren<T>(){return default(T);}
        public T[] GetComponentsInChildren<T>(){return new T[0];}
        public T[] GetComponentsInChildren<T>(bool inc){return new T[0];}
        public T AddComponent<T>() where T : Component {return default(T);}
    }
    public class Behaviour : Component { public bool enabled; public bool isActiveAndEnabled; }
    public class MonoBehaviour : Behaviour {
        public Coroutine StartCoroutine(IEnumerator r){return null;}
        public Coroutine StartCoroutine(string s){return null;}
        public void StopCoroutine(Coroutine c){}
        public void StopCoroutine(IEnumerator c){}
        public void StopCoroutine(string s){}
        public void StopAllCoroutines(){}
        public void Invoke(string s, float t){}
        public static T FindObjectOfType<T>() where T : Object {return default(T);}
    }
    public class Coroutine {}
    public class YieldInstruction {}
    public class WaitForSeconds : YieldInstruction { public WaitForSeconds(float s){} }
    public class WaitForEndOfFrame : YieldInstruction {}
    public class GameObject : Object {
        public GameObject(){} public GameObject(string n){} public GameObject(string n, params Type[] t){}
        public Transform transform; public string tag; public int layer;
        public bool activeSelf, activeInHierarchy;
        public void SetActive(bool a){}
        public T AddComponent<T>() where T : Component {return default(T);}
        public Component AddComponent(Type t){return null;}
        public T GetComponent<T>(){return default(T);}
        public T GetComponentInChildren<T>(){return default(T);}
        public T[] GetComponentsInChildren<T>(bool inc){return new T[0];}
        public static GameObject CreatePrimitive(PrimitiveType t){return null;}
    }
    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }
    public class Transform : Component, IEnumerable {
        public Vector3 position, localPosition, localScale, lossyScale, eulerAngles, localEulerAngles;
        public Quaternion rotation, localRotation;
        public Transform parent, root;
        public int childCount;
        public Vector3 forward, up, right;
        public void SetParent(Transform p){} public void SetParent(Transform p, bool w){}
        public Transform GetChild(int i){return null;}
        public Transform Find(string n){return null;}
        public void SetAsFirstSibling(){} public void SetAsLastSibling(){} public void SetSiblingIndex(int i){} public int GetSiblingIndex(){return 0;}
        public void LookAt(Vector3 v){} public void LookAt(Transform t){}
        public void SetPositionAndRotation(Vector3 p, Quaternion r){}
        public void Rotate(float x, float y, float z){}
        public void Rotate(float x, float y, float z, Space s){}
        public void Rotate(Vector3 axis, float angle, Space s){}
        public Vector3 TransformPoint(Vector3 v){return v;}
        public Vector3 InverseTransformPoint(Vector3 v){return v;}
        public IEnumerator GetEnumerator(){return null;}
    }
    public class RectTransform : Transform {
        public Vector2 anchorMin, anchorMax, pivot, sizeDelta, anchoredPosition, offsetMin, offsetMax;
        public Rect rect;
    }
    public static class Random {
        public static float value { get { return 0; } }
        public static int Range(int a, int b){return 0;}
        public static float Range(float a, float b){return 0;}
        public static Vector3 insideUnitSphere { get { return Vector3.zero; } }
    }
    public static class Time {
        public static float time, deltaTime, unscaledTime, unscaledDeltaTime, fixedDeltaTime, timeScale, realtimeSinceStartup;
    }
    public struct Resolution { public int width, height, refreshRate; }
    public static class Screen {
        public static int width, height;
        public static bool fullScreen;
        public static Resolution currentResolution;
        public static SleepTimeout sleepTimeout;
        public static ScreenOrientation orientation;
        public static void SetResolution(int w, int h, bool fs){}
    }
    public struct SleepTimeout { public static SleepTimeout NeverSleep, SystemSetting; }
    public enum ScreenOrientation { Portrait, LandscapeLeft }
    public enum NetworkReachability { NotReachable, ReachableViaCarrierDataNetwork, ReachableViaLocalAreaNetwork }
    public static class Application {
        public static int targetFrameRate;
        public static bool isPlaying, isMobilePlatform, isEditor;
        public static RuntimePlatform platform;
        public static string absoluteURL;
        public static NetworkReachability internetReachability;
        public static event System.Action<string> deepLinkActivated;
        public static void OpenURL(string url){}
        public static void Quit(){}
    }
    public static class Input {
        public static bool GetKeyDown(KeyCode k){return false;}
        public static bool GetKeyUp(KeyCode k){return false;}
        public static bool GetKey(KeyCode k){return false;}
        public static bool GetMouseButtonDown(int b){return false;}
        public static bool GetMouseButtonUp(int b){return false;}
        public static bool GetMouseButton(int b){return false;}
        public static Vector3 mousePosition { get { return Vector3.zero; } }
        public static int touchCount { get { return 0; } }
        public static Touch GetTouch(int i){return default(Touch);}
    }
    public enum KeyCode { Space, Escape, Return }
    public enum RuntimePlatform { Android, IPhonePlayer, WindowsPlayer, OSXPlayer }
    public static class PlayerPrefs {
        public static bool HasKey(string k){return false;}
        public static string GetString(string k){return null;}
        public static string GetString(string k, string d){return d;}
        public static void SetString(string k, string v){}
        public static void Save(){}
        public static void DeleteAll(){}
    }
    public static class JsonUtility {
        public static string ToJson(object o){return "";}
        public static T FromJson<T>(string s){return default(T);}
    }
    public static class Debug {
        public static void Log(object o){} public static void LogWarning(object o){} public static void LogError(object o){}
        public static bool isDebugBuild;
    }
    public static class SystemInfo { public static bool supportsVibration; public static int systemMemorySize; public static int processorCount; public static int graphicsMemorySize; public static string deviceModel; }
    public static class Handheld { public static void Vibrate(){} }
    public static class GUIUtility { public static string systemCopyBuffer; }
    public static class QualitySettings {
        public static ShadowQuality shadows; public static ShadowResolution shadowResolution;
        public static float shadowDistance;
        public static int antiAliasing, shadowCascades, vSyncCount;
    }
    public enum ShadowQuality { Disable, HardOnly, All }
    public enum ShadowResolution { Low, Medium, High, VeryHigh }
    public static class RenderSettings {
        public static bool fog; public static FogMode fogMode; public static Color fogColor;
        public static float fogStartDistance, fogEndDistance, fogDensity;
        public static Color ambientLight;
        public static Rendering.AmbientMode ambientMode;
    }
    public enum FogMode { Linear=1, Exponential=2, ExponentialSquared=3 }
    public enum ColorSpace { Gamma, Linear }
    public class AndroidJavaObject : IDisposable {
        public AndroidJavaObject(string cls, params object[] a){}
        public T Call<T>(string m, params object[] a){return default(T);}
        public void Call(string m, params object[] a){}
        public T GetStatic<T>(string f){return default(T);}
        public void Dispose(){}
    }
    public class AndroidJavaClass : AndroidJavaObject {
        public AndroidJavaClass(string cls) : base(cls) {}
    }
    public static class AudioSettings { public static int outputSampleRate; }
    public class AudioListener : Behaviour {}
    public class AudioSource : Behaviour {}
    public class RequireComponent : Attribute { public RequireComponent(Type t){} }
}
