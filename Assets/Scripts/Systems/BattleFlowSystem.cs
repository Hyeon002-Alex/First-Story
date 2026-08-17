using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;  // 프로토타입 로그용. 로직 계산엔 미사용

// 글로벌 턴 루프 9단계 소유, 조율. 각 단계에서 하위 시스템 호출
public sealed class BattleFlowSystem
{
    private readonly IReadOnlyList<AllyUnit> _allies;
    private readonly List<EnemyUnit> _enemies;           // 웨이브 전환 시 WaveSystem이 내용 교체. 참조는 불변
    private readonly IntentSystem _intentSystem;
    private readonly ProtectionSystem _protection;       // 6단계 선언, 8단계 소거. ActionResolver와 같은 인스턴스
    private readonly IActionExecutor _executor;          // 실행 구멍
    private readonly WaveSystem _waveSystem;             // 9단계 웨이브 전환 판정에서 호출

    private int _turnNum;

    public BattleFlowSystem(
        IReadOnlyList<AllyUnit> allies,
        List<EnemyUnit> enemies,
        IntentSystem intentSystem,
        ProtectionSystem protection,
        IActionExecutor executor,
        WaveSystem waveSystem)
    { 
        _allies = allies ?? throw new ArgumentNullException(nameof(allies));
        _enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
        _intentSystem = intentSystem ?? throw new ArgumentNullException(nameof(intentSystem));
        _protection = protection ?? throw new ArgumentNullException(nameof(protection));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _waveSystem = waveSystem ?? throw new ArgumentNullException(nameof(waveSystem));
        _turnNum = 0;
    }

    public int TurnNum => _turnNum;

    // 한 글로벌 턴 = 9단계 순차 실행. 판정 결과 반환 -> 상위 루프가 계속/종료 제어
    public BattleOutcome ExecuteTurn()
    {
        Step1_TurnStart();
        Step2_RecoverAP();
        Step3_AssignEnemyIntent();   // 더미 intent
        Step4_Reveal();
        Step5_InfoResponse();
        Step6_DefenseResponse();
        Step7_ExecuteBySpeed();      // 골조 — 정렬 1회 + 유효성 재검사
        Step8_TurnEnd();
        return Step9_Judge();
    }

    // 1. 턴 시작. 턴번호 증가 + 전원 행동완료 플래그 리셋
    private void Step1_TurnStart()
    { 
        _turnNum++;
        foreach (BattleUnit u in AllUnits())
        {
            u.SetActed(false);
        }
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
            if (enemy.IsIncapacitated || enemy.Skills.Count == 0)
                continue;

            SkillData skill = enemy.Skills[0];
            if (skill == null)  // 미할당 슬롯 방어
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

    // 6. 방어 대응. 보호 = _protection.SetProtect / 방향방어 = _ally.SetStance(방향, None)
    // 아군 입력 UI 미구현 -> 실제 선언 소스 없음. 소수 도착 시 여기서 배선
    private void Step6_DefenseResponse()
        => Debug.Log("[6 방어대응] 미구현(보호, 방향방어 배선 대기");

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

            _executor.Execute(command, _turnNum);
            actor.SetActed(true);

            // 실행후 사망, 붕괴 반영은 다음 순번 재검사가 자동 처리
        }
    }

    // 8. 턴종료. 순서: 지속피해 틱 -> 전투불능 -> 지속감소/만료 -> 전투상태 만료
    // 틱이 감소보다 앞이라 마지막 틱 보존. 승패/웨이브 판정은 9단계
    private void Step8_TurnEnd()
    {
        List<BattleUnit> all = AllUnits().ToList();

        // 1. 지속피해 틱. StatusEffectSystem은 순수라, 틱 전 HP 기록해 손실분만 여기서 로그
        Dictionary<BattleUnit, int> hpBefore = new Dictionary<BattleUnit, int>();
        foreach (BattleUnit u in all)
        {
            hpBefore[u] = u.CurrHP;
        }

        StatusEffectSystem.TickAll(all, _turnNum);

        foreach (BattleUnit u in all)   // 순회는 all 순서. hpBefore은 키 조회만
        {
            int lost = hpBefore[u] - u.CurrHP;
            if (lost > 0)
                Debug.Log($"  [8 지속피해] {UnitId(u)} -{lost} -> HP {u.CurrHP}/{u.MaxHP}");
        }

        // 2. 사망.전투불능. 틱으로 HP0 도달 유닛. 규칙은 UncapacitationSystem 단일 소유
        foreach (BattleUnit u in all)
        {
            if (IncapacitationSystem.CheckAndTransition(u))
                Debug.Log($"[8 전투불능] {UnitId(u)}");
        }

        // 3, 4. 상태이상 지속감소 + 만료제거
        StatusEffectSystem.DecrementAndExpire(all, _turnNum);

        // 5. 전투상태 만료: 보호 소거 + 붕괴 받는피해증가 만료(적) + 아군 방어 만료
        _protection.ClearAll();
        foreach (EnemyUnit e in _enemies)
        { 
            BreakCrackSystem.ExpireDamageMod(e, _turnNum);
        }
        // 아군 방향방어 = 한 턴 한정. 적 패시브 자세는 스킬 지속이라 zlear 대상 아님
        foreach (AllyUnit a in _allies)
        { 
            a.ClearStance();
        }
       
        Debug.Log("[8 턴종료] 상태이상 틱/전이/감소/만료 + 전투상태 만료");
    }

    // 9. 판정 4갈래. 전멸 -> 승리/웨이브전환 -> 페이즈 -> 진행
    // 전멸, 승리 순서가 동시 HP0을 패배로 처리
    private BattleOutcome Step9_Judge()
    {
        // 전멸: 전 아군 전투불능 -> 패배
        if (_allies.All(a => a.IsIncapacitated))
        {
            Debug.Log("[9 판정] 전 아군 전투불능 -> 패배");
            return BattleOutcome.Defeat;
        }

        // 현 웨이브 적 전멸
        if (_enemies.All(e => e.IsIncapacitated))
        {
            if (_waveSystem.HasNextWave)
            {
                _waveSystem.AdvanceToNextWave();    // 전투는 계속
                return BattleOutcome.Ongoing;
            }
            Debug.Log("[9 판정] 마지막 웨이브 적 전멸 -> 승리");
            return BattleOutcome.Victory;
        }

        // 페이즈 전환: 동일 보스 유지, 적 생존 중 게이지/자세 리셋. 웨이브 전환과 다른 경로
        // 트리거가 미확정이라 false
        if (CheckPhaseTransition())
        { 
            // 확정 시 리셋 로직 삽입. 전투 계속
            return BattleOutcome.Ongoing;
        }

        // 그 외: 다음 턴
        return BattleOutcome.Ongoing;
    }

    // 페이즈 전환 트리거
    // 확정 시 보스 전용 시스템이 조건을 채움. 리셋 통로도 그때 추가
    private bool CheckPhaseTransition()
    {
        return false;
    }

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
            // 붕괴 행동취소: intent 무효 표시된 적은 스킵
            if (_intentSystem.IsCancelled(enemy))
            {
                reason = "붕괴 행동취소";
                return false;
            }
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
        }

        reason = null;
        return true;
    }
    // 행위자 -> 이번 차례 명령서. 적 = intent 반환, 아군 = 입력 미구현
    // 적 분기의 intent 비null은 IsStillValid 선행 통과에 의존하는 숨은 선행조검
    // -> 순서 뒤바뀜 대비 재확인. null이면 명령서 없음 처리
    private ActionCommand BuildCommand(BattleUnit actor)
    {
        if (actor is EnemyUnit enemy)
        {
            EnemyIntent intent = _intentSystem.GetIntent(enemy);
            if(intent == null)
                return null;    // IsStillValid가 이미 걸러야 정상. 순서 역전 대비 방어
            return ActionCommand.CreateSkill(enemy, intent.Skill, intent.Target);
        }
        if (actor is AllyUnit ally)
            return ActionCommand.CreateEndTurn(ally);   // 입력 대기. 미구현. 골격은 자동 차례종료
        return null;
    }

    // 전체 유닛. 스텝1 플래그 리셋용
    private IEnumerable<BattleUnit> AllUnits()
    {
        foreach (AllyUnit a in _allies)
        { 
            yield return a;
        }
        foreach (EnemyUnit e in _enemies)
        {
            yield return e;
        }
    }

    // === === //
    private static string UnitId(BattleUnit unit)
       => unit is AllyUnit a ? a.UnitId : (unit is EnemyUnit e ? e.EnemyId : "없음");
}