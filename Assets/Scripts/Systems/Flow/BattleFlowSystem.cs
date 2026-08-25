using System;
using System.Collections;   // 코루틴 전환
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
    private readonly EnemyBehaviorSystem _behaviorSystem;   // 3단계 적 intent 결정. 더미 대체(G-3)

    private int _turnNum;

    // 코루틴 ExecuteTurn은 값을 return하지 못하므로 판정 결과를 여기 담음
    // 드라이버는 한 턴 펌프 완료 후 이 값을 읽어 계속/종료를 판단
    // 첫 MoveNext 전 오독 방지 기본값 = Ongoing
    public BattleOutcome LastOutcome { get; private set; } = BattleOutcome.Ongoing;

    public BattleFlowSystem(
        IReadOnlyList<AllyUnit> allies,
        List<EnemyUnit> enemies,
        IntentSystem intentSystem,
        ProtectionSystem protection,
        IActionExecutor executor,
        WaveSystem waveSystem,
        EnemyBehaviorSystem behaviorSystem)
    { 
        _allies = allies ?? throw new ArgumentNullException(nameof(allies));
        _enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
        _intentSystem = intentSystem ?? throw new ArgumentNullException(nameof(intentSystem));
        _protection = protection ?? throw new ArgumentNullException(nameof(protection));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _waveSystem = waveSystem ?? throw new ArgumentNullException(nameof(waveSystem));
        _behaviorSystem = behaviorSystem ?? throw new ArgumentNullException(nameof(behaviorSystem));
        _turnNum = 0;
    }

    public int TurnNum => _turnNum;

    // 한 글로벌 턴 = 9단계 순차 실행. 코루틴 전환: 값 대신 LastOutcome에 판정 기록
    // 입력 필요 단계는 하위 스텝이 요청을 yield -> 여기서 재-yield로 드라이버에 전달
    public IEnumerator ExecuteTurn()
    {
        Step1_TurnStart();
        Step2_RecoverAP();
        Step3_AssignEnemyIntent();
        Step4_Reveal();

        // 5. 정보대응: 하위 스텝의 요청을 그대로 밖으로 재-yield
        IEnumerator info = Step5_InfoResponse();
        while (info.MoveNext())
        { 
            yield return info.Current;
        }

        // 6. 방어대응: 하위 스텝의 요청을 그대로 밖으로 재-yield
        IEnumerator defense = Step6_DefenseResponse();
        while (defense.MoveNext())
        {
            yield return defense.Current;
        }

        // 7. 속도순 행동: 아군 차례마다 행동 요청을 재-yield
        IEnumerator action = Step7_ExecuteBySpeed();
        while (action.MoveNext())
        {
            yield return action.Current;
        }

        Step8_TurnEnd();
        LastOutcome = Step9_Judge();
        yield break;
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

    // 3. 적 intent 결정. 실제 AI: 각 적 behavior.Decide -> IntentSystem 등록
    // ClearAll은 여기서(턴 경계 초기화 = 턴 루프 소유). 결정·등록은 EnemyBehaviorSystem
    private void Step3_AssignEnemyIntent()
    {
        _intentSystem.ClearAll();

        // 스냅샷 = 생존 필터 완료본(계약). LivingAllies = 공격 대상 후보(플레이어측),
        // LivingEnemies = 결정 주체 진영(적측, SurvivingAllyAtLeast 조건이 봄)
        BattleSnapshot snapshot = new BattleSnapshot(
            _turnNum,
            _allies.Where(a => !a.IsIncapacitated).ToList(),
            _enemies.Where(e => !e.IsIncapacitated).ToList());

        _behaviorSystem.DecideAll(snapshot);
    }

    // 4. 공개. UI 후순위. 골격은 대상, 방향만 읽어 로그
    private void Step4_Reveal()
    {
        foreach (var pair in _intentSystem.AllIntents)
            Debug.Log($"[4 공개] {pair.Key.EnemyId}: 방향 {pair.Value.Skill.Direction} 대상 {UnitId(pair.Value.Target)}");
    }

    // 5. 정보 대응. 생존 아군마다 정보대응 요청을 밖으로 내밀고
    // 드라이버가 채운 응답이 정보형 고유행동이면 InfoResponseSystem이 검증, 적용
    // void IEnumerator: 아군마다 yield return req -> ExecuteTurn이 드라이버에 전달
    // 정보대응은 방어 전. EndTurn/부적격 종류는 무시(정보 대응 포기)
    private IEnumerator Step5_InfoResponse()
    {
        foreach (AllyUnit a in _allies)
        {
            if (a.IsIncapacitated)
                continue;   // 전투불능 아군은 대응 요청 대상 아님

            InputRequest req = new InputRequest(InputPhase.Info, a);
            yield return req;   // 드라이버가 req.SetResponse로 응답 슬롯을 채운 뒤 재개

            if (!req.IsAnswered)
                continue;   // 드라이버 무응답 방어. 정상 펌프는 항상 채움

            ActionCommand response = req.Response;
            // 정보형 고유행동만 적용. 집합 판정은 InfoResponseSystem 단일 소유
            if (InfoResponseSystem.IsInfoResponse(response))
                InfoResponseSystem.TryApply(response, _intentSystem);
        }
    }

    // 6. 방어 대응. 생존 아군마다 방어대응 요청을 밖으로 내밀고
    // 드라이버가 채운 응답을 ResponsePhaseSystem이 검중, 적용
    // void -> IEnumerator: 아군마다 yield return req -> ExecuteTurn이 드라이버에 전달
    // 응답이 방향방어/보호면 적용, EndTurn/기타 종류는 무시
    private IEnumerator Step6_DefenseResponse()
    {
        foreach (AllyUnit a in _allies)
        {
            if (a.IsIncapacitated || a.ActedThisTurn)
                continue;   // 전투불능 또는 정보대응으로 이미 행동한 아군은 대응 대상 아님

            InputRequest req = new InputRequest(InputPhase.Defense, a);
            yield return req;   // 드라이버가 req.SetResponse로 응답 슬롯을 채운 뒤 재개

            if (!req.IsAnswered)
                continue;   // 드라이버 무응답 방어. 정상 펌프는 항상 채움

            ActionCommand response = req.Response;
            // 방어대응 유효 종류만 적용. 집합 판정은 ResponsePhaseSystem 단일 소유
            // EndTurn = 대응 포기. 그 외 부적격 종류는 IsResponseKind가 걸러 무시
            if (ResponsePhaseSystem.IsResponseKind(response.Kind))
                ResponsePhaseSystem.TryApply(response, _protection);
        }
    }

    // 7. 속도순 실행. 정렬 1회 + 순서고정, 유효성 재검사
    // void -> IEnumerator: 아군 차례에 행동 요청을 yield -> 드라이버 응답을 진행
    // 적 차례는 intent에서 명령을 즉시 만들어 왕복 없이 실행
    private IEnumerator Step7_ExecuteBySpeed()
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

            ActionCommand command;
            if (actor is AllyUnit ally)
            {
                // 아군: 행동단계 요청을 밖으로 내밀고 드라이버 응답을 명령으로 사용
                InputRequest req = new InputRequest(InputPhase.Action, ally);
                yield return req;   // 드라이버가 req.SetResponse로 채운 뒤 재개

                if (!req.IsAnswered)
                    continue;       // 무응답 방어. 정상 펌프는 항상 채움
                command = req.Response;
            }
            else
            { 
                // 적: intent -> 명령. null이면 명령 없음
                command = BuildCommand((EnemyUnit)actor);
                if (command == null)
                    continue;
            }

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

        // tuple 요소명 밍시화
        return participants
            .Select((unit, slotIndex) => (unit: unit, slotIndex: slotIndex))
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
        // 6단계 방어대응으로 이미 행동한 아군은 속도순 행동 스킵
        if (actor.ActedThisTurn)
        {
            reason = "대응행동 완료";
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
    // 행위자 -> 이번 차례 명령서. 적 전용: intent -> 스킬 명령
    // 아군 분기 제거: 아군 행동은 스텝7이 InputRequest 왕복으로 직접 처리. BuildCommand 미경유
    // intent 비null은 IsStillVaild 선행 통과에 의존하는 숨은 선행조건 -> 순서역전 대비 재확인
    private ActionCommand BuildCommand(EnemyUnit enemy)
    {
        EnemyIntent intent = _intentSystem.GetIntent(enemy);
        if (intent == null)
            return null;    // IsStillValid가 이미 걸러야 정상. 순서 역전 대비 방어
        return ActionCommand.CreateSkill(enemy, intent.Skill, intent.Target);
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