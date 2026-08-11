using System;

// 한 적의 이번 턴 의도. 불변, 생성 시 확정, 이후 바뀌지 않음
// 방향, 행동명, 부가효과, 회피불가는 전부 Skill에서 파생
public sealed class EnemyIntent
{ 
    public SkillData Skill { get; }      // 쓸 스킬. 방향 등 파생 정보의 진실원
    public BattleUnit Target { get; }    // 예정 대상. 상황 의존값이라 별도 저장

    public EnemyIntent(SkillData skill, BattleUnit target)
    { 
        Skill = skill ?? throw new ArgumentNullException(nameof(skill));
        Target = target;    // 대상 null 허용: 대상 상실, 범위 등은 실행 시점이 판정
    }
}