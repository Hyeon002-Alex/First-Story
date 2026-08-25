// 스킬 대상의 진영. 행동자 기준 상대값. 절대적 Ally/Enemy 아님
// 아군이 Hostile 사용 -> 적, 적이 Hostile 사용 -> 아군. 같은 SkillData를 양쪽이 공유
// Self는 TargetRule.Self가 전담 -> 여기 값 무시
public enum TargetSide
{ 
    Friendly,   // 행동자와 같은 편
    Hostile     // 행동자와 다른 편
}