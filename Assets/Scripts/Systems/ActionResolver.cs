using System;
using System.Collections.Generic;
using UnityEngine;

// 한 행동 실행 파이프 소유, 조율. IActionExecutor 구현
// 계산/반정/적옹은 전부 하위 시스템 위임
public sealed class ActionResolver : IActionExecutor
{
    private readonly IReadOnlyList<AllyUnit> _allies;
    private readonly IReadOnlyList<EnemyUnit> _enemies;
    private readonly ProtectionSystem _protection;      // 대상 파이프 3스텝에서 읽음. BattleFlowSystem과 같은 인스턴스
    private readonly IntentSystem _intentSystem;        // 붕괴취소

    public ActionResolver(
        IReadOnlyList<AllyUnit> allies, 
        IReadOnlyList<EnemyUnit> enemies, 
        ProtectionSystem protection,
        IntentSystem intentSystem)
    {
        _allies = allies ?? throw new ArgumentNullException(nameof(allies));
        _enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
        _protection = protection ?? throw new ArgumentNullException(nameof(protection));
        _intentSystem = intentSystem ?? throw new ArgumentNullException(nameof(intentSystem));
    }

    // === IActionExecutor: BattleFlowSystem 7단계가 각 행위자 차례에 호출 === //
    // currentTurn = 붕괴 만료턴 기준
    public void Execute(ActionCommand command, int currentTurn)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        // 스킬 없는 명령서: 차례종료. 대응행동은 턴루프 5~6단계 소유라 원칙상 여기 없음
        if (command.Skill == null)
        {
            Debug.Log($"[실행] {Name(command.Actor)} {command.Kind} (스킬 없음 -> 파이프 미실행");
            return;
        }

        // 1~4. 대상 결정
        TargetingResult targeting = TargetingSystem.Resolve(command, _allies, _enemies, _protection);
        if (targeting.TargetLost)
        {
            Debug.Log($"[실행] {Name(command.Actor)} {command.Skill.SkillId} -> 대상 상실, 행동 취소");
            return;
        }

        Debug.Log($"[실행] {Name(command.Actor)} {command.Skill.SkillId} (대상 {targeting.Targets.Count}명)");

        // 대상별 루프: 범위면 각 대상마다 5~12
        foreach (BattleUnit target in targeting.Targets)
        {
            ResolvePerTarget(command.Actor, target, command.Skill, currentTurn);
        }
    }

    // 대상 1명 5~12 스텝
    private void ResolvePerTarget(BattleUnit actor, BattleUnit target, SkillData skill, int currentTurn)
    {
        // 5. 회피 판정: 공격 계열만 대상. 회복/보호막은 회피 무관
        // 회피불가 스킬이 아니고 대상이 회피 보유 -> 1 즉시소모. 이 대상 통째 무효
        if (HasDamage(skill) && !skill.IsUnavoidable && EvasionSystem.HasEvasion(target))
        {
            EvasionSystem.Consume(target);
            Debug.Log($"  {Name(target)} 회피. 스킬 무효 (남은 회피 {target.EvasionCount})");
            return;     // 6 ~ 12 전부 스킵: 피해/회복/보호막/상태이상 모두 무효
        }

        int attack = actor.EffectiveAttack;

        // --- 피해. 방어/방향/붕괴 보정 있음 --- //
        if (HasDamage(skill))
        {
            // 6~7. 방향 배율 판정 + 계산
            DamageResult dmg = ComputeDamage(actor, target, skill);

            // 8. 적용: 보호막 흡수 -> HP
            DamageApplication applied = DamageSystem.Apply(target, dmg.FinalDamage);
            Debug.Log($"  {Name(target)} 피해 {dmg.FinalDamage} (흡수 {applied.ShieldAbsorbed}/HP {applied.HPLost})" +
                $" [dir {dmg.DirectionMod} brk {dmg.BreakMod}] HP {target.CurrHP}/{target.MaxHP}");

            // 9. 사망판정. 감지 로그만
            if (target.CurrHP == 0)
            {
                Debug.Log($"  {Name(target)} HP 0");
            }
            // 10~11. 생존 시 붕괴/균열 누적, 발생. 사망 동시 발생 X
            else if (target is EnemyUnit enemy && skill.BreakAmount > 0)
            {
                BreakCrackSystem.Accumulate(enemy, skill.BreakAmount);
                bool broke = BreakCrackSystem.CheckAndTrigger(enemy, currentTurn, _intentSystem);
                if (broke)
                    Debug.Log($"  {Name(enemy)} {(enemy.IsBoss ? "균열" : "붕괴")}! 받는피해 x1.50" +
                        (_intentSystem.IsCancelled(enemy) ? " + 예정행동 취소" : ""));
            }
        }

        // --- 회복 --- //
        if (HasHealing(skill))
        {
            // 회복 계수 변동은 미구현

            int heal = CombatCalculator.CalcHealing(attack, skill.HealingCoeffi, skill.FixedHealing, 1.00f);
            HealingSystem.Apply(target, heal);
            Debug.Log($"  {Name(target)} 회복 {heal} -> HP {target.CurrHP}/{target.MaxHP}");
        }

        // --- 보호막 --- //
        if (HasShield(skill))
        {
            int shield = CombatCalculator.CalcShield(attack, skill.ShieldCoeffi, skill.FixedShield);
            ShieldSystem.Grant(target, shield);
            Debug.Log($"  {Name(target)} 보호막 +{shield} -> 총 {target.Shield}");
        }

        // 12. 상태이상: 부여 + 정화. 회피 시 5스텝 return이 이 블록까지 통째 스킵(피해+동반 상태이상 무효)
        // 피해 없는 디버프는 회피 게이트 밖이라 여기 도달. 확정 부여
        foreach (StatusEffectData effect in skill.Effects)
        { 
            StatusEffectSystem.Apply(target, effect, actor, currentTurn);
            Debug.Log($"  {Name(target)} 상태이상 부여: {effect.StatusId}");
        }
        if (skill.CleansesNormalStatus)
        {
            StatusEffectSystem.Cleanse(target);
            Debug.Log($"  {Name(target)} 일반 상태이상 회복");
        }
    }

    // === 피해 계산 단일 경로 === //
    // 미리보기와 실제가 똑같이 이걸 호출
    private DamageResult ComputeDamage(BattleUnit actor, BattleUnit target, SkillData skill)
    {
        // 6. 방향 배율: 대상의 실제 방어 자세 조회. 자세 없으면 None -> 1.00
        float directionMod = DirectionSystem.GetMod(skill.Direction, target.GetDefenseStance());
        // 붕괴 받는 피해 증가: 대상이 이미 붕괴/균열 상태면 1.50
        float breakMod = BreakCrackSystem.GetDamageMod(target);

        // 7. 계산
        return CombatCalculator.CalcDamage(
            actor.EffectiveAttack, skill.DamageCoeffi, skill.FixedDamage,
            target.EffectiveDefense, directionMod, breakMod);
    }

    // 미리보기: 실제와 동일 계산. 적용만 안함. UI 예상피해 표시가 이걸 호출
    public DamageResult PreviewDamage(BattleUnit actor, BattleUnit target, SkillData skill)
    {
        if (actor == null) 
            throw new ArgumentNullException(nameof(actor));
        if (target == null) 
            throw new ArgumentNullException(nameof(target));
        if (skill == null) 
            throw new ArgumentNullException(nameof(skill));

        return ComputeDamage(actor, target, skill);
    }

    // === wide SkillData 효과 유무(구조 부채: 한 스킬이 피해/회복/보호막 복수 보유 가능) === //
    private static bool HasDamage(SkillData s) => s.DamageCoeffi != 0f || s.FixedDamage != 0;
    private static bool HasHealing(SkillData s) => s.HealingCoeffi != 0f || s.FixedHealing != 0;
    private static bool HasShield(SkillData s) => s.ShieldCoeffi != 0f || s.FixedShield != 0;

    private static string Name(BattleUnit u)
        => u is AllyUnit a ? a.UnitId : (u is EnemyUnit e ? e.EnemyId : "?");
}
