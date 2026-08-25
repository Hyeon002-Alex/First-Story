using System.Collections.Generic;
using System.Linq;

// 아군의 선택지 산출. 순수 조회
// 제안과 집행의 분리: 여기는 무엇을 고를 수 있나만 답하고
// -> 실제 검증/적용은 InfoRevealSystem/ResponsePhaseSystem/ActionResolver 소유
// 공유 규칙은 그쪽 단일 소유를 읽음
public static class ChoiceQuerySystem
{
    // 아군 1명의 이번 위상 선택지. snapshot = 생존 필터 완료본(계약)
    public static AllyChoices GetChoices(AllyUnit ally, InputPhase phase, BattleSnapshot snapshot)
    {
        List<ActionChoice> choices;
        switch (phase)
        {
            case InputPhase.Info:
                choices = InfoChoices(ally, snapshot);
                break;
            case InputPhase.Defense:
                choices = DefenseChoices(ally, snapshot);
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
    private static List<ActionChoice> DefenseChoices(AllyUnit ally, BattleSnapshot snapshot)
    {
        var list = new List<ActionChoice>();

        int defenseCost = ResponsePhaseSystem.DefenseAPCost;
        int protectionCost = ResponsePhaseSystem.ProtectionAPCost;

        // 방향방어: 상/중/하 각각 하나의 오퍼. 대상 없음
        if (APSystem.CanAfford(ally, defenseCost))
        {
            list.Add(ActionChoice.Defense(AttackDirection.High, defenseCost));
            list.Add(ActionChoice.Defense(AttackDirection.Mid, defenseCost));
            list.Add(ActionChoice.Defense(AttackDirection.Low, defenseCost));
        }

        // 보호: 대상 = 생존 아군 중 자기 제외. 후보 0이면 오퍼 생략(자기보호 금지)
        if (APSystem.CanAfford(ally, protectionCost))
        {
            List<BattleUnit> allyTargets = LivingAllyPool(snapshot, exclude: ally);
            if (allyTargets.Count > 0)
                list.Add(ActionChoice.Protection(protectionCost, allyTargets));
        }

        // [target-side 확정 후 수렴] 방어대응 가능 스킬(도발 등)은 데이터·진영 미정 -> 자리
        return list;
    }

    // 속도순 행동: 고유행동(공격형)/편성스킬/아이템/차례종료
    // 스킬 대상 산출 보류
    // -> TargetRule은 단일/범위/자신 만 담고 어느 진영인지는 데이터에 없음
    // 실행은 명령의 Target 진영을 따라가면 되지만, 오퍼는 대상 미선택 상태라 진영을 알아야 함
    // [Action seam 수렴] 공격 오퍼의 대상별 예상피해(ActionResolver.PreviewDamage)는
    // 공격 오퍼가 생기는 시점(Action 위상 스킬 대상 산출 수렴)에 함께 부착.
    // K-3은 예상=실제 불변식만 확정, 부착 대상(공격 오퍼)이 없어 필드 미노출(M1)
    private static List<ActionChoice> ActionChoices(AllyUnit ally, BattleSnapshot snapshot)
    {
        return new List<ActionChoice>();   // 현재 위상 산출 = 차례종료(공통 경로가 추가)뿐
    }

    // === 대상 규칙 해소 === //
    // 규칙별 지정대상 후보 산출. pool = 겨냥 진영의 생존 유닛(호출자가 진영 결정해 주입)
    // 진영 지식을 이 함수 밖에 둠: target-side가 데이터에 없어 진영은 호출 맥락이 앎(보호=아군, 정보확인=적)
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

    private static List<BattleUnit> LivingAllyPool(BattleSnapshot snapshot, AllyUnit exclude)
        => snapshot.LivingAllies.Where(a => !ReferenceEquals(a, exclude)).Cast<BattleUnit>().ToList();
}