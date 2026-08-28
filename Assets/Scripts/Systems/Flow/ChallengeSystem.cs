using System;
using System.Collections.Generic;
using UnityEngine;

// 도전(칼리프)의 "다음 글로벌 턴 대상 강제" 예약 보관/적용. 상태 있음 -> 인스턴스
// Protection과 다른 메커니즘: 실행시점 리다이렉트가 아닌
// -> intent 결정 직수/공개 전에 intent 자체를 교체
public sealed class ChallengeSystem
{
    public static bool IsChallengeSkill(SkillData skill)
        => skill != null && skill.IsChallenge;

    private sealed class Reservation
    { 
        public AllyUnit Caster { get; }
        public int TargetTurn { get; }
        public Reservation(AllyUnit caster, int targetTurn)
        {
            Caster = caster;
            TargetTurn = targetTurn;
        }
    }

    private readonly Dictionary<EnemyUnit, Reservation> _reservations = new Dictionary<EnemyUnit, Reservation>();

    public void Register(AllyUnit caster, EnemyUnit target, int usedTurn)
    {
        if (caster == null) 
            throw new ArgumentNullException(nameof(caster));
        if (target == null) 
            throw new ArgumentNullException(nameof(target));

        _reservations[target] = new Reservation(caster, usedTurn + 1);
        Debug.Log($"[도전 예약] {caster.UnitId} -> {target.EnemyId}, 적용턴 {usedTurn + 1}");
    }

    // Step3 직후, Step4 전에 호출. 적용 조건: TargetRule.Single
    public void ApplyReservations(IntentSystem intentSystem, int currentTurn)
    {
        if (intentSystem == null) 
            throw new ArgumentNullException(nameof(intentSystem));
        if (_reservations.Count == 0) 
            return;

        List<EnemyUnit> resolved = new List<EnemyUnit>();
        foreach (KeyValuePair<EnemyUnit, Reservation> kv in _reservations)
        {
            if (kv.Value.TargetTurn > currentTurn)
                continue;

            resolved.Add(kv.Key);

            EnemyIntent intent = intentSystem.GetIntent(kv.Key);
            if (intent == null || intent.Skill.TargetRule != TargetRule.Single)
            {
                Debug.Log($"[도전 만료] {kv.Key.EnemyId} 유도 가능한 단일 직접 공격 없음");
                continue;
            }

            intentSystem.SetIntent(kv.Key, new EnemyIntent(intent.Skill, kv.Value.Caster));
            Debug.Log($"[도전 발동] {kv.Key.EnemyId} 예정 대상 -> {kv.Value.Caster.UnitId}");
        }

        foreach (EnemyUnit e in resolved)
        { 
            _reservations.Remove(e);
        }
    }
}