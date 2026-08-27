using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
    public enum AmbientMode { Skybox=0, Trilight=1, Flat=3, Custom=4 }
    public enum DefaultReflectionMode { Skybox, Custom }
    public enum BlendMode { Zero, One, DstColor, SrcColor, OneMinusDstColor, SrcAlpha,
                            OneMinusSrcColor, DstAlpha, OneMinusDstAlpha, SrcAlphaSaturate, OneMinusSrcAlpha }
    public enum RenderQueue { Background=1000, Geometry=2000, AlphaTest=2450, Transparent=3000, Overlay=4000 }
    public enum IndexFormat { UInt16, UInt32 }
    public enum GraphicsDeviceType { OpenGLES2, OpenGLES3, Vulkan, Metal, Direct3D11 }
}

namespace UnityEngine
{
    public enum CubemapFace { Unknown=-1, PositiveX, NegativeX, PositiveY, NegativeY, PositiveZ, NegativeZ }
    public class Cubemap : Texture {
        public Cubemap(int size, TextureFormat fmt, bool mips){}
        public void SetPixels(Color[] px, CubemapFace face){}
        public void Apply(bool mips){}
    }
    public class Texture : Object {
        public FilterMode filterMode; public TextureWrapMode wrapMode; public int anisoLevel;
        public int width, height;
    }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Repeat, Clamp, Mirror }
    public enum TextureFormat { RGBA32, ARGB32, RGB24, Alpha8 }
    public enum RenderTextureFormat { ARGB32, Default }
    public enum RenderTextureReadWrite { Default, Linear, sRGB }
    public class Texture2D : Texture {
        public Texture2D(int w, int h){}
        public Texture2D(int w, int h, TextureFormat f, bool mips){}
        public Texture2D(int w, int h, TextureFormat f, bool mips, bool linear){}
        public void SetPixels(Color[] c){}
        public Color[] GetPixels(){return new Color[0];}
        public void SetPixel(int x,int y,Color c){}
        public Color GetPixel(int x,int y){return Color.white;}
        public void Apply(){} public void Apply(bool mips){}
        public void ReadPixels(Rect r, int x, int y){}
    }
    public class RenderTexture : Texture {
        public RenderTexture(int w,int h,int d){}
        public int antiAliasing;
        public static RenderTexture active;
        public static RenderTexture GetTemporary(int w,int h,int d,RenderTextureFormat f,RenderTextureReadWrite rw){return null;}
        public static void ReleaseTemporary(RenderTexture rt){}
    }
    public class Sprite : Object {
        public Texture2D texture;
        public static Sprite Create(Texture2D t, Rect r, Vector2 pivot){return null;}
        public static Sprite Create(Texture2D t, Rect r, Vector2 pivot, float ppu, uint extrude,
                                    SpriteMeshType type, Vector4 border){return null;}
    }
    public enum SpriteMeshType { FullRect, Tight }
    public class Shader : Object {
        public static Shader Find(string n){return null;}
    }
    public class Material : Object {
        public Material(Shader s){} public Material(Material m){}
        public Color color; public Texture mainTexture; public Shader shader;
        public int renderQueue;
        public MaterialGlobalIlluminationFlags globalIlluminationFlags;
        public void SetFloat(string n, float v){} public float GetFloat(string n){return 0;}
        public void SetInt(string n, int v){}
        public void SetColor(string n, Color c){} public Color GetColor(string n){return Color.white;}
        public void SetTexture(string n, Texture t){} public Texture GetTexture(string n){return null;}
        public void SetTextureScale(string n, Vector2 v){} public void SetTextureOffset(string n, Vector2 v){}
        public bool HasProperty(string n){return true;}
        public void EnableKeyword(string k){} public void DisableKeyword(string k){}
    }
    [Flags] public enum MaterialGlobalIlluminationFlags { None=0, RealtimeEmissive=1, BakedEmissive=2, EmissiveIsBlack=4 }
    public class Mesh : Object {
        public Vector3[] vertices, normals; public Vector2[] uv; public int[] triangles;
        public Color[] colors; public Vector4[] tangents;
        public int subMeshCount; public Bounds bounds;
        public Rendering.IndexFormat indexFormat;
        public void SetVertices(List<Vector3> v){} public void SetNormals(List<Vector3> v){}
        public void SetUVs(int ch, List<Vector2> v){} public void SetTriangles(List<int> t, int sub){}
        public void SetTriangles(int[] t, int sub){}
        public int[] GetTriangles(int sub){return new int[0];}
        public void SetIndices(int[] idx, MeshTopology t, int sub){}
        public void RecalculateNormals(){} public void RecalculateBounds(){} public void RecalculateTangents(){}
        public void MarkDynamic(){} public void Clear(){}
    }
    public enum MeshTopology { Triangles, Quads, Lines, LineStrip, Points }
    public class MeshFilter : Component { public Mesh mesh, sharedMesh; }
    public class Renderer : Component {
        public Material material, sharedMaterial;
        public Material[] materials, sharedMaterials;
        public Rendering.ShadowCastingMode shadowCastingMode;
        public bool receiveShadows, enabled;
    }
    public class MeshRenderer : Renderer {}
    public class Camera : Behaviour {
        public static Camera main;
        public bool orthographic; public float orthographicSize;
        public float nearClipPlane, farClipPlane, fieldOfView, aspect;
        public CameraClearFlags clearFlags; public Color backgroundColor;
        public int cullingMask; public RenderTexture targetTexture;
        public bool allowMSAA, allowHDR;
        public void Render(){}
        public Vector3 WorldToScreenPoint(Vector3 v){return v;}
        public Vector3 WorldToViewportPoint(Vector3 v){return v;}
    }
    public enum CameraClearFlags { Skybox=1, Color=2, SolidColor=2, Depth=3, Nothing=4 }
    public class Light : Behaviour {
        public LightType type; public Color color; public float intensity, range, spotAngle;
        public LightShadows shadows; public float shadowStrength, shadowBias, shadowNormalBias, shadowNearPlane;
    }
    public enum LightType { Spot, Directional, Point, Area }
    public enum LightShadows { None, Hard, Soft }
    public class Font : Object {
        public Material material;
        public static Font CreateDynamicFontFromOSFont(string[] names, int size){return null;}
        public static Font CreateDynamicFontFromOSFont(string name, int size){return null;}
        public void RequestCharactersInTexture(string s, int size, FontStyle style){}
        public bool GetCharacterInfo(char c, out CharacterInfo info, int size, FontStyle style){info=new CharacterInfo();return true;}
    }
    public struct CharacterInfo { public float advance; public int size; }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter,
                             MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum TextAlignment { Left, Center, Right }
    public class TextMesh : Component {
        public string text; public Font font; public int fontSize; public FontStyle fontStyle;
        public float characterSize, lineSpacing, offsetZ;
        public TextAnchor anchor; public TextAlignment alignment; public Color color; public bool richText;
    }
    public static class Resources {
        public static T GetBuiltinResource<T>(string p) where T : Object {return default(T);}
        public static T Load<T>(string p) where T : Object {return default(T);}
    }
    public class CanvasRenderer : Component {
        public void SetAlpha(float a){} public float GetAlpha(){return 1;}
    }
    public class RectOffset { public RectOffset(){} public RectOffset(int l,int r,int t,int b){}
        public int left,right,top,bottom; }
    public class CanvasGroup : Behaviour { public float alpha; public bool interactable, blocksRaycasts, ignoreParentGroups; }
}
