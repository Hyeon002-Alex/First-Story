// 일반 적 유닛 규칙의 조건 종류
public enum ConditionKind
{ 
    Always,                 // 무조건 진행
    SelfHPBelow,            // 자기 HP 비율이 임계 이하일 때
    TurnNumberMod,          // N턴마다
    TurnNumberAtLeast,      // 일정 턴 이후
    SurvivingAllyAtLeast,   // 적 생존 수가 임계 이상일 때
}