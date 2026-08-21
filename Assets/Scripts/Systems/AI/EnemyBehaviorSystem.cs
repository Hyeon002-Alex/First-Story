using System;
using System.Collections.Generic;
using UnityEngine;

// 3단계 적 intent 결정 조율. 적별 behavior 바인딩 + Decide -> IntentSystem.SetIntent
// 결정 소유. 보관(IntentSystem)과 분리 — 여긴 SetIntent만. 테이블 초기화(ClearAll)는 턴 루프(BattleFlow) 소유
// behaviorPatternId(문자열) -> BehaviorPatternData 매핑을 레지스트리 주입으로. EnemyUnitData 스키마 불변
// lazy bind: Decide 시 캐시 미스면 팩토리. 웨이브 새 적 자동 대응, 보스 인스턴스 유지(phaseIndex 보존)
public sealed class EnemyBehaviorSystem
{
    private readonly IReadOnlyDictionary<string, BehaviorPatternData> _patternRegistry;
    private readonly IntentSystem _intentSystem;
    // 적별 behavior 인스턴스 캐시. 참조 키. 같은 적 = 같은 behavior(stateful 보스 상태 보존)
    private readonly Dictionary<EnemyUnit, IEnemyBehavior> _behaviors = new Dictionary<EnemyUnit, IEnemyBehavior>();

    public EnemyBehaviorSystem(
        IReadOnlyDictionary<string, BehaviorPatternData> patternRegistry,
        IntentSystem intentSystem)
    {
        _patternRegistry = patternRegistry ?? throw new ArgumentNullException(nameof(patternRegistry));
        _intentSystem = intentSystem ?? throw new ArgumentNullException(nameof(intentSystem));
    }

    // 3단계: 생존 적 전원 결정 -> IntentSystem 등록. ClearAll은 호출 전 BattleFlow가 수행(턴 경계 초기화)
    public void DecideAll(BattleSnapshot snapshot)
    {
        foreach (EnemyUnit enemy in snapshot.LivingEnemies)
        {
            // 스냅샷이 생존 필터 완료본(계약)이나 방어적 이중 확인. 계약 위반 시에도 죽은 적 미결정 보장
            if (enemy.IsIncapacitated)
                continue;

            IEnemyBehavior behavior = GetOrBind(enemy);
            if (behavior == null)
                continue;   // 바인딩 실패(패턴 미등록/보스 미구현) -> 미등록. 이 적 이번 턴 무행동

            EnemyIntent intent = behavior.Decide(snapshot, enemy);
            if (intent == null)
                continue;   // 결정 불가(대상 후보 없음·폴백 규칙 누락) -> 미등록

            _intentSystem.SetIntent(enemy, intent);
            Debug.Log($"[3 Intent] {enemy.EnemyId} -> {intent.Skill.SkillId} (대상 {(intent.Target != null ? "있음" : "없음")})");
        }
    }

    // 캐시 조회, 없으면 팩토리 생성 후 캐시
    private IEnemyBehavior GetOrBind(EnemyUnit enemy)
    {
        if (_behaviors.TryGetValue(enemy, out IEnemyBehavior cached))
            return cached;

        IEnemyBehavior behavior = CreateBehavior(enemy);
        if (behavior != null)
            _behaviors[enemy] = behavior;   // 실패(null)는 캐시 안 함 -> 데이터 고치면 다음 턴 재시도
        return behavior;
    }

    // behaviorPatternId -> 구현. 유일한 일반몹/보스 분기점. 이후 코드는 IEnemyBehavior만 봄
    private IEnemyBehavior CreateBehavior(EnemyUnit enemy)
    {
        if (enemy.IsBoss)
        {
            // 보스 골격(BossBehaviorBase)은 열림. 단 v0.1.0 공개범위(프롤로그~4장)에 페이즈 보스 실물 없음
            // 실물 등장 시 여기에 patternId -> 파생 보스 클래스(Chapter17System 등) 매핑 추가
            // 그때까지 등록할 파생이 없어 무행동 유지
            Debug.LogWarning($"[G] {enemy.EnemyId}: 보스 behavior 실물 미등록(G-4 골격만). 이번 턴 무행동");
            return null;
        }

        string patternId = enemy.BehaviorPatternID;
        if (string.IsNullOrEmpty(patternId) || !_patternRegistry.TryGetValue(patternId, out BehaviorPatternData pattern))
        {
            Debug.LogWarning($"[G] {enemy.EnemyId}: 패턴 '{patternId}' 레지스트리 미등록. 이번 턴 무행동");
            return null;
        }

        return new DataDrivenBehavior(pattern);
    }
}