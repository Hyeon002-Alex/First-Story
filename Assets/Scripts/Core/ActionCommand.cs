using System;

// 행동 명령서. 순수 데이터, 실행 로직 없음
// 생성자 private, 팩토리 통로로만 생성
public sealed class ActionCommand
{ 
    public ActionKind Kind { get; }
    public BattleUnit Actor { get; }
    public BattleUnit Target { get; }           // 필요한 종류만. 대상 파이프가 최종 확정
    public SkillData Skill { get; }             // 고유, 스킬, 아이템만. 아니면 null
    public AttackDirection Direction { get; }   // 방향방어만. 아니면 None

    // 팩토리 전용 생성자
    private ActionCommand(ActionKind kind, BattleUnit actor, BattleUnit target, SkillData skill, AttackDirection direction)
    { 
        Kind = kind;
        Actor = actor;
        Target = target;
        Skill = skill;
        Direction = direction;
    }

    // === 대응행동 === //
    public static ActionCommand CreateDefense(BattleUnit actor, AttackDirection direction)
    {
        RequireActor(actor);
        if(direction == AttackDirection.None)
            throw new ArgumentException("방어는 High/Mid/Low만 허용", nameof(direction));
        return new ActionCommand(ActionKind.Defense, actor, null, null, direction);
    }
    public static ActionCommand CreateProtection(BattleUnit actor, BattleUnit target)
        => new ActionCommand(ActionKind.Protection, RequireActor(actor),
                target ?? throw new ArgumentNullException(nameof(target)), null, AttackDirection.None);
    public static ActionCommand CreateReveal(BattleUnit actor, BattleUnit target)
         => new ActionCommand(ActionKind.Reveal, RequireActor(actor),
                target ?? throw new ArgumentNullException(nameof(target)), null, AttackDirection.None);

    // === 실행행동 === //
    // 스킬계열: actor, skill 필수. target은 대상 파이프 몫이라 막지 않음
    public static ActionCommand CreateUnique(BattleUnit actor, SkillData skill, BattleUnit target)
        => new ActionCommand(ActionKind.UniqueAction, RequireActor(actor), target, RequireSkill(skill), AttackDirection.None);
    public static ActionCommand CreateSkill(BattleUnit actor, SkillData skill, BattleUnit target)
       => new ActionCommand(ActionKind.Skill, RequireActor(actor), target, RequireSkill(skill), AttackDirection.None);
    public static ActionCommand CreateItem(BattleUnit actor, SkillData item, BattleUnit target)
        => new ActionCommand(ActionKind.Item, RequireActor(actor), target, RequireSkill(item), AttackDirection.None);
    public static ActionCommand CreateEndTurn(BattleUnit actor)
        => new ActionCommand(ActionKind.EndTurn, RequireActor(actor), null, null, AttackDirection.None);

    // === 가드 === //
    private static BattleUnit RequireActor(BattleUnit actor)
        => actor ?? throw new ArgumentNullException(nameof(actor));
    private static SkillData RequireSkill(SkillData skill)
        => skill ?? throw new ArgumentNullException(nameof(skill));
}