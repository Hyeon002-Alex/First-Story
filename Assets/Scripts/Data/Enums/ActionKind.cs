// 행동 종류. 아군 실행행동 + 대응단계행동을 하나의 명령서로 통합, enum으로 구분
// 대응 = 방향방어, 보호, 정보확인 / 실행 = 고유, 스킬, 아이템, 차례종료
public enum ActionKind
{ 
    UniqueAction,   // 고유행동
    Skill,          // 스킬
    Item,           // 아이템. SkillData로 표현, apCost = 0
    Defense,        // 방향방어. 상, 중, 하
    Protection,     // 보호. 보호자 -> 피보호자
    Reveal,         // 정보확인
    EndTurn         // 차례종료
}