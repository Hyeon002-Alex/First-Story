// 스킬이 대상을 고르는 규칙. 실제 선전 로직은 TargetimgSystem
// v0.1.0 확정분. 스킬이 늘면 값 추가 가능
public enum TargetRule
{ 
    Single,         // 단일 대상
    Area,           // 범위
    FixedTarget,    // 고정 대상. 대상 변경 면역
    Self,           // 자기 자신
}