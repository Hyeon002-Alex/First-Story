using System;
using System.Collections.Generic;

// 아군이 지금 고를 수 있는 행동 1건. 순수데이터, 실행 로직 없음
// UI가 이걸 나열해 보여주고, 사용자가 하나+대상을 골라 ActionCommand로 확정
// 팩토리 전용: 종류별로 유효 필드 조합이 달라, 생성 통로를 나눠 모순 상태를 차단
public sealed class ActionChoice
{ 
    public ActionKind Kind { get; }
    public SkillData Skill { get; }             // 스킬 계열만. 방향방어/보호/차례종료는 null
    public AttackDirection Direction { get; }   // 방향방어만. 그외 None
    public int ApCost { get; }
    // 지정 가능한 대상 후보. 사용자가 이 중 하나를 고름
    // 대상 없는 행동은 빈 목록
    public IReadOnlyList<BattleUnit> ValidTargets { get; }
    // 대상별 무방어 예상피해. ActionResolver.PreviedDamage와 동일 경로 -> 예상=실제 보장
    // 피해 없는 스킬/대응행동은 빈 목록
    public IReadOnlyDictionary<BattleUnit, DamageResult> PreviewDamages { get; }
    
    private static readonly Dictionary<BattleUnit, DamageResult> NoPreview
        = new Dictionary<BattleUnit, DamageResult>();

    private ActionChoice(
        ActionKind kind, SkillData skill, AttackDirection direction,
        int apCost, IReadOnlyList<BattleUnit> validTargets,
        IReadOnlyDictionary<BattleUnit, DamageResult> previewDamages)
    {
        Kind = kind;
        Skill = skill;
        Direction = direction;
        ApCost = apCost;
        ValidTargets = validTargets;
        PreviewDamages = previewDamages ?? NoPreview;   // 빈 프리뷰의 단일 소유 지점. 팩토리들은 여기 위임
    }

    // 방향방어 1방향. 대상 없음
    // previewDamages: 이 방향을 고르면 각 공격자(EnemyUnit, BattleUnit으로 저장)에게 받을 예상피해(M)
    // 미확인 공격자 게이팅은 호출부(DefensePreviewSystem 경유) 책임 -> 여기선 안 가림. null이면 NoPreview
    public static ActionChoice Defense(
        AttackDirection direction, int apCost,
        IReadOnlyDictionary<BattleUnit, DamageResult> previewDamages = null)
    {
        if (direction == AttackDirection.None)
            throw new ArgumentException("방향방어는 High/Mid/Low만", nameof(direction));
        return new ActionChoice(
            ActionKind.Defense, null, direction, apCost, Array.Empty<BattleUnit>(), previewDamages);
    }

    // 보호. 대상 = 지킬 아군 후보
    public static ActionChoice Protection(int apCost, IReadOnlyList<BattleUnit> allyTargets)
        => new ActionChoice(
            ActionKind.Protection, null, AttackDirection.None, apCost, RequireTargets(allyTargets), NoPreview);

    // 정보형 고유행동. 대상 = 정보확인할 적 후보. 예상피해는 이 통로엔 아직 안 붙임
    public static ActionChoice InfoAction(SkillData skill, int apCost, IReadOnlyList<BattleUnit> enemyTargets)
        => new ActionChoice(
            ActionKind.UniqueAction, skill ?? throw new ArgumentNullException(nameof(skill)),
            AttackDirection.None, apCost, RequireTargets(enemyTargets), NoPreview);

    // 고유행동(비정보형). 속도순 행동 단계 전용. Kind는 InfoAction과 같은 UniqueAction이지만
    // 통로를 나눠 호출부에서 위상 혼동을 방지
    // previewDamages: 호출부가 HasDamage 게이팅 후 넘김
    public static ActionChoice UniqueAction(
        SkillData skill, int apCost, IReadOnlyList<BattleUnit> targets,
        IReadOnlyDictionary<BattleUnit, DamageResult> previewDamages)
         => new ActionChoice(
            ActionKind.UniqueAction, skill ?? throw new ArgumentNullException(nameof(skill)),
            AttackDirection.None, apCost, RequireTargets(targets), previewDamages);

    // 편성 스킬 1개. 대상 후보는 스킬의 TargetRule/TargetSide 해석 결과
    public static ActionChoice EquippedSkill(
        SkillData skill, int apCost, IReadOnlyList<BattleUnit> targets,
        IReadOnlyDictionary<BattleUnit, DamageResult> previewDamages)
        => new ActionChoice(
            ActionKind.Skill, skill ?? throw new ArgumentNullException(nameof(skill)),
            AttackDirection.None, apCost, RequireTargets(targets), previewDamages);

    // 차례종료(포기). 전 위상 공통. 대상 없음, 비용 0
    public static ActionChoice EndTurn()
        => new ActionChoice(
            ActionKind.EndTurn, null, AttackDirection.None, 0, Array.Empty<BattleUnit>(), NoPreview);

    // 대상 필수 오퍼의 후보 0 방어. 후보 0이면 호출 측이 오퍼를 만들지 말았어야 함
    private static IReadOnlyList<BattleUnit> RequireTargets(IReadOnlyList<BattleUnit> targets)
    {
        if (targets == null || targets.Count == 0)
            throw new ArgumentException("대상 필수 오퍼는 후보가 최소 1명이어야 함", nameof(targets));
        return targets;
    }
}