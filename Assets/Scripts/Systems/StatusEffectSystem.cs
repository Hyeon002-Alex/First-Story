using System;
using System.Collections.Generic;

// 상태이상 부여/중첩/정화/면역/틱/만료 소유. 상태 없음. 목록은 각 유닛 RuntimeStats에 저장
// 집계는 소유 안 함. RuntimeStats가 소유해 유닛 접근자와 공유
public static class StatusEffectSystem
{
    // 부여: 면역 -> 스냅샷 -> 중첩 or 신규. 확정 적용(확률, 저항 없음)
    public static void Apply(BattleUnit target, StatusEffectData def, BattleUnit caster, int currentTurn)
    { 
        if(target == null)
            throw new ArgumentNullException(nameof(target));
        if(def == null) 
            throw new ArgumentNullException(nameof(def));
        if(caster == null) 
            throw new ArgumentNullException(nameof(caster));

        // 면역: 적만. 우선 아군 면역 없음
        if (target is EnemyUnit enemy && IsImune(enemy, def))
            return;

        // 지속피해 스탭샷: 부여 시 caster 공격력으로 1회 계산해 고정. 지속피해 없으면 0
        int snapshot = ComputeDamageSnapshot(def, caster);

        RuntimeStatusEffect existing = target.FindStatusEffect(def.StatusId);
        if (existing != null)
        {
            // 중첩: 합산 안 함. 더 강한 스냅샷 유지 + 더 긴 지속으로 갱신
            // 적용턴은 갱신 안 함. 진행 중 지연틱 리셋 방지
            existing.RefreshSnapShot(snapshot);
            existing.RefreshDuration(def.BaseDuration);
        }
        else
        { 
            target.AddStatusEffect(new RuntimeStatusEffect(def, currentTurn, def.BaseDuration, snapshot));
        }
    }

    // 정화: 일반 전부 제거. 정화 스킬/아이템 공용. 특수는 면제
    public static void Cleanse(BattleUnit target)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        // 순회 중 제거 회피: 대상 먼저 수집 후 제거
        List<RuntimeStatusEffect> toRemove = new List<RuntimeStatusEffect>();
        foreach (RuntimeStatusEffect e in target.StatusEffects)
        {
            if (e.Definition.Category == StatusEffectCategory.Normal)
                toRemove.Add(e);
        }
        foreach (RuntimeStatusEffect e in toRemove)
        { 
            target.RemoveStatusEffect(e);
        }
    }

    // === 8단계 턴종료 === //

    // 8단계 1. 지속피해 틱. 적용 다음 턴부터(Istickable). 전체 유닛은 호출측이 넘김
    // HP 감소는 DamageSystem 통로 직행 -> 보호막 먼저 흡수, 방어/방향/붕괴 무보정
    public static void TickAll(IEnumerable<BattleUnit> units, int currentTurn)
    { 
        if(units == null)
            throw new ArgumentNullException(nameof(units));
        foreach (BattleUnit u in units)
        { 
            TickOne(u, currentTurn);
        }
    }

    private static void TickOne(BattleUnit unit, int currentTurn)
    {
        // DamageSystem.Apply는 상태이상목록을 안 건드림. HP/보호막만 -> 순회중 호출 안전
        foreach (RuntimeStatusEffect e in unit.StatusEffects)
        { 
            // 지속피해 조각 있는 인스턴스만 스냅샷 > 0. 적용 턴 인스턴스는 통째 스킵
            if(e.DamageSnapshot >0 && e.IsTickable(currentTurn))
                DamageSystem.Apply(unit, e.DamageSnapshot);
        }
    }

    // 8단계 3, 4. 지속시간 감소 + 만료 제거. 적용턴 지난 인스턴스만
    // 틱과 동일 게이트: 부여 턴은 감소 안 함 -> 지속시간 온전히 카운트, 마지막 틱 보존
    public static void DecrementAndExpire(IEnumerable<BattleUnit> units, int currentTurn)
    { 
        if(units == null)
            throw new ArgumentNullException(nameof(units));
        foreach (BattleUnit u in units)
        { 
            DecrementAndExpireOne(u, currentTurn);
        }
    }

    private static void DecrementAndExpireOne(BattleUnit unit, int curentTurn)
    { 
        // 순회 중 제거 회피: 만료분 먼저 수집
        List<RuntimeStatusEffect> expired = new List<RuntimeStatusEffect>();
        foreach (RuntimeStatusEffect e in unit.StatusEffects)
        {
            if (!e.IsTickable(curentTurn))
                continue;   // 부여 턴 스킵(적용턴 == 현재턴)
            e.Decrement();
            if (e.IsExpired)
                expired.Add(e);
        }
        foreach (RuntimeStatusEffect e in expired)
        { 
            unit.RemoveStatusEffect(e);
        }
    }

    // ClearNormal

    // 면역 판정: 정적 목록에 def 참조 포함 여부
    private static bool IsImune(EnemyUnit enemy, StatusEffectData def)
    {
        foreach (StatusEffectData imune in enemy.StatudImmunities)
        { 
            if(imune == def) 
                return true;
        }
        return false;
    }

    // 지속피해 조각 magnitude로 스냅샷 계산. 방어/방향/붕괴 무보정
    private static int ComputeDamageSnapshot(StatusEffectData def, BattleUnit caster)
    {
        foreach (EffectComponent c in def.Components)
        {
            if (c.EffectKind == EffectKind.DamageOverTime)
                return Math.Max(0, (int)Math.Floor(caster.EffectiveAttack * (double)c.Magnitude));
        }
        return 0;
    }
}