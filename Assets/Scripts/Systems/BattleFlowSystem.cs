using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;  // 프로토타입 로그용. 로직 계산엔 미사용

// 글로벌 턴 루프 9단계 소유, 조율. 각 단계에서 하위 시스템 호출
// 미설계 단계(5, 6, 8, 9 + 7실행)은 구멍. 로그 인터페이스 자리만
public sealed class BattleFlowSystem
{
    private readonly IReadOnlyList<AllyUnit> _allies;
    private readonly IReadOnlyList<EnemyUnit> _enemies;
    private readonly IntentSystem _intentSystem;
    private readonly IActionExecutor _executor;         // 실행 구멍
    private readonly Func<string, SkillData> _skillResolver;    // skillId 해석

    private int _turnNum;

    public BattleFlowSystem(
        IReadOnlyList<AllyUnit> allies,
        IReadOnlyList<EnemyUnit> enemies,
        IntentSystem intentSystem,
        IActionExecutor executor,
        Func<string, SkillData> skillResolver)
    { 
        _allies = allies ?? throw new ArgumentNullException(nameof(allies));
        _enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
        _intentSystem = intentSystem ?? throw new ArgumentNullException(nameof(intentSystem));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _skillResolver = skillResolver ?? throw new ArgumentNullException(nameof(skillResolver));
        _turnNum = 0;
    }

    public int TurnNum => _turnNum;

    // 한 글로벌 턴 = 9단계 순차 실행
    public void ExecuteTurn()
    {
        Step1_TurnStart();
        Step2_RecoverAP();
        Step3_AssignEnemyIntent();   // 더미 intent
        Step4_Reveal();
        Step5_InfoResponse();
        Step6_DefenseResponse();
        Step7_ExecuteBySpeed();      // 골조 — 정렬 1회 + 유효성 재검사
        Step8_TurnEnd();
        Step9_Judge();
    }

    // 1. 턴 시작. 턴번호 증가
    private void Step1_TurnStart()
    { 
        _turnNum++;
        Debug.Log($"===[턴 {_turnNum} 시작] ===");
    }

    // 2. AP 회복. 전체 아군 넘김, 전투불능 스킵은 APSystem 내부
    private void Step2_RecoverAP()
    {
        APSystem.RecoverAll(_allies);
        Debug.Log($"[2 AP회복 {string.Join(", ", _allies.Select(a => $"{a.UnitId}:{a.CurrAP}"))}");
    }

    // 3. 적 intent 생성. 더미: 각 적에게 첫 skillId + 첫 생존 아군
    // 매 턴 새로 파생
    private void Step3_AssignEnemyIntent()
    {
        _intentSystem.ClearAll();
        AllyUnit firstLivingAlly = _allies.FirstOrDefault(a => !a.IsIncapacitated);

        foreach (EnemyUnit enemy in _enemies)
        {
            if (enemy.IsIncapacitated || enemy.SkillIds.Count == 0)
                continue;

            SkillData skill = _skillResolver(enemy.SkillIds[0]);
            if (skill == null)
                continue;

            _intentSystem.SetIntent(enemy, new EnemyIntent(skill, firstLivingAlly));
            Debug.Log($"[3 Intent] {enemy.EnemyId} -> {skill.SkillId} 대상 {(firstLivingAlly != null ? firstLivingAlly.UnitId : "없음")}");
        }
    }

    // 4. 공개. UI 후순위. 골격은 대상, 방향만 읽어 로그
    private void Step4_Reveal()
    {
        foreach (var pair in _intentSystem.AllIntents)
            Debug.Log($"[4 공개] {pair.Key.EnemyId}: 방향 {pair.Value.Skill.Direction} 대상 {UnitId(pair.Value.Target)}");
    }

    // 5. 정보 대응
    private void Step5_InfoResponse()
        => Debug.Log("[5 정보대응] 미구현");
    // 6. 방어 대응
    private void Step6_DefenseResponse()
        => Debug.Log("[6 방어대응] 미구현");

    // 7. 속도순 실행. 정렬 1회 + 순서고정, 유효성 재검사 스킵
    private void Step7_ExecuteBySpeed()
    {
        List<BattleUnit> order = BuildOrder();
        Debug.Log($"[7 정렬] {string.Join(" > ", order.Select(u => $"{UnitId(u)}(속{u.EffectiveSpeed})"))}");

        foreach (BattleUnit actor in order)
        {
            // 유효성 재검사. 리스트는 건드리지 않고 유효한지만 물음
            if (!IsStillValid(actor, out string reason))
            {
                Debug.Log($"[7 스킵 {UnitId(actor)} - {reason}");
                continue;
            }

            // 적: intent / 아군: 입력 미구현
            ActionCommand command = BuildCommand(actor);
            if (command == null)
                continue;

            // 실행 = 미구현
            _executor.Execute(command);

            // 실행후 사망, 붕괴 반영은 다음 순번 재검사가 자동 처리
        }
    }

    // 8. 턴종료. 미구현: 지속피해, 지속시간, 만료
    private void Step8_TurnEnd()
        => Debug.Log("[8 턴종료] 구멍(묶음3)");

    // 9. 판정. 미구현: 전멸, 승리, 웨이브 전환
    private void Step9_Judge() 
        => Debug.Log("[9 판정] 구멍(묶음3)");

    // === 7단계 보조 === //
    // 참여자 = 잔체 아군 + 전체 적. 아군 먼저 -> 슬롯 인덱스 순
    // 정렬: 유효속도 내림차순. 동률이면 슬롯 인덱스 오름차순 -> 완전 결정론
    private List<BattleUnit> BuildOrder()
    {
        var participants = new List<BattleUnit>(_allies.Count + _enemies.Count);
        participants.AddRange(_allies);
        participants.AddRange( _enemies);

        return participants
            .Select((unit, slotIndex) => (unit, slotIndex))
            .OrderByDescending(x => x.unit.EffectiveSpeed)
            .ThenBy(x => x.slotIndex)
            .Select(x => x.unit)
            .ToList();
    }
    // 순번이 왔을 때 아직 행동 유효한가
    private bool IsStillValid(BattleUnit actor, out string reason)
    {
        if (actor.IsIncapacitated)
        {
            reason = "전투불능";
            return false;
        }
        if (actor is EnemyUnit enemy)
        {
            EnemyIntent intent = _intentSystem.GetIntent(enemy);
            if (intent == null)
            {
                reason = "intent 없음";
                return false;
            }
            if (intent.Target != null && intent.Target.IsIncapacitated)
            {
                reason = "대상 상실";
                return false;
            }
            // 붕괴 취소 판정. 미구현
        }

        reason = null;
        return true;
    }
    // 행위자 -> 이번 차례 명령서. 적 = intent 반환, 아군 = 입력 미구현
    private ActionCommand BuildCommand(BattleUnit actor)
    {
        if (actor is EnemyUnit enemy)
        {
            EnemyIntent intent = _intentSystem.GetIntent(enemy);
            return ActionCommand.CreateSkill(enemy, intent.Skill, intent.Target);
        }
        if (actor is AllyUnit ally)
            return ActionCommand.CreateEndTurn(ally);   // 입력 대기. 미구현. 골격은 자동 차례종료
        return null;
    }

    // === === //
    private static string UnitId(BattleUnit unit)
       => unit is AllyUnit a ? a.UnitId : (unit is EnemyUnit e ? e.EnemyId : "없음");
}