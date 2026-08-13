using System;
using System.Collections.Generic;
using System.Linq;

// 최종 대상 산출. 순수 조회. 상태 바꾸지 않음
// 대상 파이프 4스텝: 후보(targetingRule + 예정대상) -> 고정대상 -> 보호 리다이렉트 -> 생존
// 예정대상 = command.Target. 적은 BuildCommand가 intent.Target을 여기 실어 옴
// 스킬 계열 명령서만 취급. 대응행동은 파이프 밖
public static class TargetingSystem
{
    public static TargetingResult Resolve(
        ActionCommand command,
        IReadOnlyList<AllyUnit> allies,
        IReadOnlyList<EnemyUnit> enemies,
        ProtectionSystem protection)
    { 
        if(command == null)
            throw new ArgumentNullException(nameof(command));
        if(allies == null)
            throw new ArgumentNullException(nameof(allies));
        if(enemies == null)
            throw new ArgumentNullException(nameof(enemies));
        if(command.Skill == null)
            throw new ArgumentException("대상 결정은 스킬 계열 명령서만",  nameof(command));

        TargetRule rule = command.Skill.TargetRule;

        // 자기 자신
        if (rule == TargetRule.Self)
            return new TargetingResult(new List<BattleUnit> { command.Actor }, false);

        // 1. 후보 = 예정대상
        BattleUnit designated = command.Target;

        // 범위: 예정 대상의 진영 전원. 보호 리타이렉트 대상 아님
        if (rule == TargetRule.Area)
        {
            if (designated == null)
                return new TargetingResult(new List<BattleUnit>(), true);   // 대표 없음 = 대상 상실

            List<BattleUnit> pool = LivingSameFaction(designated, allies, enemies);
            return new TargetingResult(pool, pool.Count == 0);
        }

        // Single / FixedTarget
        BattleUnit finalTarget = designated;

        // 2. 고정대상 확인: FixedTarget이면 3 건너뜀
        // 3. 보호 리다이렉트: rule == Single일 때만 ProtectionSystem.GetProtector로 대상 교체
        if (rule == TargetRule.Single)
        {
            BattleUnit protector = protection.GetProtector(designated);
            if(protector != null && !protector.IsIncapacitated)
                finalTarget = protector;
        }

        // 4. 최종 대상 생존 확인. 죽었으면 대상 상실
        if (finalTarget == null || finalTarget.IsIncapacitated)
            return new TargetingResult(new List<BattleUnit>(), true);

        return new TargetingResult(new List<BattleUnit> { finalTarget }, false);
    }

    // 대표 대상과 같은 진영의 생존 유닛 전체. 명단 슬롯 순서 유지 = 결정론
    private static List<BattleUnit> LivingSameFaction(
        BattleUnit representative,
        IReadOnlyList<AllyUnit> allies,
        IReadOnlyList<EnemyUnit> enemies)
    { 
        if(representative is AllyUnit)
            return allies.Where(a => !a.IsIncapacitated).Cast<BattleUnit>().ToList();
        return enemies.Where(e => !e.IsIncapacitated).Cast<BattleUnit>().ToList();
    }
}