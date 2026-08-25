// 헤드리스 회귀용 UnityEngine 스텁 (Assets 밖 — Unity 무시).
// 시스템/데이터가 참조하는 UnityEngine 표면 전부: ScriptableObject / Debug / [SerializeField] / [CreateAssetMenu]
using System;

namespace UnityEngine
{
    // SO 상속 타입(SkillData 등)의 기저. 헤드리스에선 빈 참조 타입으로 충분
    public class ScriptableObject { }

    // 전투 로그. Muted로 프로브 실행 중 억제
    public static class Debug
    {
        public static bool Muted = false;
        public static void Log(object m) { if (!Muted) Console.WriteLine(m); }
        public static void LogWarning(object m) { if (!Muted) Console.WriteLine("[warn] " + m); }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class SerializeFieldAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string fileName;
        public string menuName;
    }
}
