namespace UnityEngine
{
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x=x; this.y=y; this.width=width; this.height=height; }
        public float xMin { get => x; set { width = xMax - value; x = value; } }
        public float xMax { get => x + width; set => width = value - x; }
        public float yMin { get => y; set { height = yMax - value; y = value; } }
        public float yMax { get => y + height; set => height = value - y; }
        public static Rect MinMaxRect(float xmin,float ymin,float xmax,float ymax)=>new Rect(xmin,ymin,xmax-xmin,ymax-ymin);
        public Vector2 center { get => new Vector2(x + width * .5f, y + height * .5f); set { x = value.x - width * .5f; y = value.y - height * .5f; } }
    }
    public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} }
    public static class Mathf
    {
        public static int Max(int a,int b)=>Math.Max(a,b); public static float Abs(float v)=>Math.Abs(v); public static float Max(float a,float b)=>Math.Max(a,b); public static float Min(float a,float b)=>Math.Min(a,b);
        public static float Clamp(float v,float min,float max)=>Math.Min(Math.Max(v,min),max); public static float Lerp(float a,float b,float t)=>a+(b-a)*t;
        public static float Round(float v)=>(float)Math.Round(v); public static int RoundToInt(float v)=>(int)Math.Round(v);
    }
    public static class Screen { public static int width=1080,height=1920; public static float dpi=160f; }
    public static class Time { public static double realtimeSinceStartupAsDouble; }
    public static class Application { public static event Action onBeforeRender; public static void RaiseBeforeRender() => onBeforeRender?.Invoke(); }
    public static class Debug { public static void LogWarning(object message){} }
    public enum RuntimeInitializeLoadType { SubsystemRegistration, BeforeSceneLoad }
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type){} }
}
