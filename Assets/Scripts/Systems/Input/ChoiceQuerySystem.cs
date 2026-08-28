using System.Collections.Generic;
using System.Linq;

// 아군의 선택지 산출. 순수 조회
// 제안과 집행의 분리: 여기는 무엇을 고를 수 있나만 답하고
// -> 실제 검증/적용은 InfoRevealSystem/ResponsePhaseSystem/ActionResolver 소유
// 공유 규칙은 그쪽 단일 소유를 읽음
public static class ChoiceQuerySystem
{
    // 아군 1명의 이번 위상 선택지. snapshot = 생존 필터 완료본(계약)
    // intentSystem = 방어위상 예상피해(M) 산출에 필요. Info/Action 위상은 안 씀, 옵션(기본 null)
    // -> null이면 DefenseChoices가 프리뷰 없이 산출(K/L 기존 호출부 무변경 보존)
    public static AllyChoices GetChoices(
        AllyUnit ally, InputPhase phase, BattleSnapshot snapshot, IntentSystem intentSystem = null)
    {
        List<ActionChoice> choices;
        switch (phase)
        {
            case InputPhase.Info:
                choices = InfoChoices(ally, snapshot);
                break;
            case InputPhase.Defense:
                choices = DefenseChoices(ally, snapshot, intentSystem);
                break;
            case InputPhase.Action:
                choices = ActionChoices(ally, snapshot);
                break;
            default:
                choices = new List<ActionChoice>();
                break;
        }

        // 차례종료(포기)는 전 위상 공통. 정보/방어에선 "대응 포기", 행동에선 "차례 넘김"
        // 기존 관례와 일치: ScriptedInputSource도 무행동을 EndTurn으로 표현
        choices.Add(ActionChoice.EndTurn());
        return new AllyChoices(ally, phase, choices);
    }

    // === 산출 === //
    // 정보 대응: 정보형 고유행동 1개(자격 + AP + 확인 대상 있을 때). 대상 = 생존 적
    private static List<ActionChoice> InfoChoices(AllyUnit ally, BattleSnapshot snapshot)
    {
        var list = new List<ActionChoice>();

        SkillData unique = ally.UniqueAction;
        // 정보형 자격 찬정은 InfoResponseSystem이 단일 소유. 제안/집행 기준 일원화
        // 오퍼 Kind는 UniqueAction 고정
        if (InfoResponseSystem.IsInfoActionSkill(unique) && APSystem.CanAfford(ally, unique.ApCost))
        {
            // 정보확인 대상 진영 = 적(자명). 규칙 해소로 후보 산출
            IReadOnlyList<BattleUnit> targets =
                GetValidTargets(ally, unique.TargetRule, LivingEnemyPool(snapshot));
            if (targets.Count > 0)
                list.Add(ActionChoice.InfoAction(unique, unique.ApCost, targets));
        }

        return list;
    }

    // 방어 대응: 방향방어 3방향 + 보호. AP 게이팅. 대응 AP는 ResponsePhaseSystem 단일 소유를 읽음
    // 방향방어 오퍼에는 방어위상 예상피해(M) 부착: 이 방향을 고르면 공격자별로 얼마나 맞는지
    // intentSystem == null이면 프리뷰 생략(K/L 기존 호출부 호환). 실사용(Step6 배선)은 UI 대주제에서 항상 넘길 것
    private static List<ActionChoice> DefenseChoices(AllyUnit ally, BattleSnapshot snapshot, IntentSystem intentSystem)
    {
        var list = new List<ActionChoice>();

        int defenseCost = ResponsePhaseSystem.DefenseAPCost;
        int protectionCost = ResponsePhaseSystem.ProtectionAPCost;

        // 방향방어: 상/중/하 각각 하나의 오퍼. 대상 없음, 프리뷰는 방향별 공격자-피해 매핑
        if (APSystem.CanAfford(ally, defenseCost))
        {
            list.Add(ActionChoice.Defense(AttackDirection.High, defenseCost,
                DefensePreviewFor(ally, AttackDirection.High, intentSystem, snapshot)));
            list.Add(ActionChoice.Defense(AttackDirection.Mid, defenseCost,
                DefensePreviewFor(ally, AttackDirection.Mid, intentSystem, snapshot)));
            list.Add(ActionChoice.Defense(AttackDirection.Low, defenseCost,
                DefensePreviewFor(ally, AttackDirection.Low, intentSystem, snapshot)));
        }

        // 보호: 대상 = 생존 아군 중 자기 제외. 후보 0이면 오퍼 생략(자기보호 금지)
        if (APSystem.CanAfford(ally, protectionCost))
        {
            List<BattleUnit> allyTargets = LivingAllyPool(snapshot, exclude: ally);
            if (allyTargets.Count > 0)
                list.Add(ActionChoice.Protection(protectionCost, allyTargets));
        }

        // 방어대응가능 자격 플래그 확정 후 수렴
        // 도발류는 SkillData 자격 플래그가 아직 없어 보류
        return list;
    }

    // 방향방어 예상피해(M) 부착. intentSystem == null(K/L 구 호출부) 또는 공격자 0명이면 null -> NoPreview로 자연 대체
    // DefensePreviewSystem은 EnemyUnit 키 반환(공격자만 있어 더 정밀) -> ActionChoice.PreviewDamages의
    // BattleUnit 키(스킬 오퍼 프리뷰와 형태 통일)로 여기서 변환. 변환 책임을 소비처(여기)에 둬 M-3을 안 건드림
    private static IReadOnlyDictionary<BattleUnit, DamageResult> DefensePreviewFor(
        AllyUnit ally, AttackDirection direction, IntentSystem intentSystem, BattleSnapshot snapshot)
    {
        if (intentSystem == null)
            return null;

        IReadOnlyDictionary<EnemyUnit, DamageResult> byEnemy =
            DefensePreviewSystem.Preview(ally, direction, intentSystem, snapshot.LivingEnemies);

        if (byEnemy.Count == 0)
            return null;

        var byUnit = new Dictionary<BattleUnit, DamageResult>();
        foreach (KeyValuePair<EnemyUnit, DamageResult> pair in byEnemy)
            byUnit[pair.Key] = pair.Value;
        return byUnit;
    }

    // 속도순 행동: 고유행동(공격형)/편성스킬 3개. 아이템은 아직 미구현, 차례종료는 공통 경로
    // 대상 진영은 skill.TargetSide로 풀을 고르고 GetValidTargets로 규칙 전용
    // 공격 오퍼 무방어 예상피해(PreviewFor)까지 이 통로에서 부착 완료
    // 아직 밖에 있는 것: 방어대응가능 스킬의 여기 노출 여부
    // -> 자격 플래그 미정, 데이터 미입력으로 대응
    private static List<ActionChoice> ActionChoices(AllyUnit ally, BattleSnapshot snapshot)
    {
        var list = new List<ActionChoice>();

        // 고유행동: 정보형은 5단계 소관. InfoChoices가 이미 산출. 여기선 제외
        SkillData unique = ally.UniqueAction;
        if(!InfoResponseSystem.IsInfoActionSkill(unique))
            TryAddSkillChoice(list, ally, unique, snapshot, isUnique: true);

        // 편성 스킬 3개
        foreach (SkillData skill in ally.EquippedSkills)
        { 
            TryAddSkillChoice(list, ally, skill, snapshot, isUnique: false);
        }

        return list;
    }

    // 스킬 1개를 검증해 오퍼로 추가. null/AP부족/후보0이면 오퍼 자체를 생략
    private static void TryAddSkillChoice(
        List<ActionChoice> list, AllyUnit ally, SkillData skill, BattleSnapshot snapshot, bool isUnique)
    {
        if (skill == null || !APSystem.CanAfford(ally, skill.ApCost))
            return;

        List<BattleUnit> pool = SidePool(skill.TargetSide, snapshot);
        IReadOnlyList<BattleUnit> targets = GetValidTargets(ally, skill.TargetRule, pool);
        if (targets.Count == 0)
            return;

        IReadOnlyDictionary<BattleUnit, DamageResult> preview = PreviewFor(ally, skill, targets);

        list.Add(isUnique
            ? ActionChoice.UniqueAction(skill, skill.ApCost, targets, preview)
            : ActionChoice.EquippedSkill(skill, skill.ApCost, targets, preview));
    }

    // 무방어 예상피해. ActionResolver.HasDamage/PreviewDamage 재사용
    // 피해 없는 스킬은 null 반환 -> ActionChoice 생성자가 NoPreview로 대체
    private static IReadOnlyDictionary<BattleUnit, DamageResult> PreviewFor(
        AllyUnit actor, SkillData skill, IReadOnlyList<BattleUnit> targets)
    {
        if (!ActionResolver.HasDamage(skill))
            return null;

        var preview = new Dictionary<BattleUnit, DamageResult>();
        foreach (BattleUnit target in targets)
            preview[target] = ActionResolver.PreviewDamage(actor, target, skill);
        return preview;
    }

    // === 대상 규칙 해소 === //
    // 규칙별 지정대상 후보 산출. pool = 겨냥 진영의 생존 유닛(호출자가 진영 결정해 주입)
    // 진영 지식을 이 함수 밖에 둠: 이 함수는 대상 "형태(TargetRule)"만 책임지고
    // -> 진영 해성은 SidePool의 관심사로 분리
    // 보호 리다이렉트는 실행(TargetingSystem) 소관 -> 오퍼는 리다이렉트 전 원 후보를 냄
    public static IReadOnlyList<BattleUnit> GetValidTargets(
        BattleUnit actor, TargetRule rule, IReadOnlyList<BattleUnit> pool)
    {
        switch (rule)
        {
            case TargetRule.Self:
                return new List<BattleUnit> { actor };   // pool 무관, 자기 자신
            case TargetRule.Single:
            case TargetRule.Area:
            case TargetRule.FixedTarget:
                // 지정대상 후보 = 진영 생존 전원. Area는 실행 시 진영 전체로 확장되나 후보 픽은 동일
                return new List<BattleUnit>(pool);
            default:
                return new List<BattleUnit>();
        }
    }

    // === pool 조립 === //
    // 생존 필터는 스냅샷이 이미 완료(계약). 여기선 진영 -> BattleUnit 목록 변환 + 슬롯 순서 유지
    private static List<BattleUnit> LivingEnemyPool(BattleSnapshot snapshot)
        => snapshot.LivingEnemies.Cast<BattleUnit>().ToList();

    // 보호 전용(자기보호 금지). Friendly/Single 스킬(회복 등)엔 쓰지 않음 -> 아래 자기포함 오버로드를 씀
    private static List<BattleUnit> LivingAllyPool(BattleSnapshot snapshot, AllyUnit exclude)
        => snapshot.LivingAllies.Where(a => !ReferenceEquals(a, exclude)).Cast<BattleUnit>().ToList();

    // 자기 포함. 회복/정화 등 Friendly 스킬은 행동자 자신도 유효 대상
    private static List<BattleUnit> LivingAllyPool(BattleSnapshot snapshot)
        => snapshot.LivingAllies.Cast<BattleUnit>().ToList();

    // TargetSide -> 실제 진영 pool. ChoiceQuerySystem은 아군 행동자만 다뤄 매핑이 고정됨
    // 적 AI가 SkillData를 재사용할 때의 행동자 기준 상대 해석은 별도 소관. 여기선 아군 -> 적/아군 뿐
    private static List<BattleUnit> SidePool(TargetSide side, BattleSnapshot snapshot)
        => side == TargetSide.Friendly ? LivingAllyPool(snapshot) : LivingEnemyPool(snapshot);
}