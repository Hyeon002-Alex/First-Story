using System;
using System.Collections.Generic;

// 적 intent 일괄 보관, 공개. 상태 있음 -> 인스턴스 클래스. APSystem과 반대
// 유닛에 intent를 실음: 대응단계, UI가 살아있는 모든 적 intent를 한 곳에서 순회하려고
public sealed class IntentSystem
{ 
    // 적별 의도. 참조 키로 조회
    private readonly Dictionary<EnemyUnit, EnemyIntent> _intents = new Dictionary<EnemyUnit, EnemyIntent>();
    // 정보 확인 플래그: intent와 분리 소유. intent는 불변으로 유지
    private readonly HashSet<EnemyUnit> _revealed = new HashSet<EnemyUnit>();
    private readonly HashSet<EnemyUnit> _cancelled = new HashSet<EnemyUnit>();  // 붕괴 행동취소 표시

    // === 등록, 소거 === //
    // 적 intent 등록. 같은 적 재등록 시 덮어씀
    // null 가드: null 저장 시 미등록과 null 등록이 GetIntent에서 구분 불가
    public void SetIntent(EnemyUnit enemy, EnemyIntent intent)
    {
        if (enemy == null)
            throw new ArgumentNullException(nameof(enemy));
        if (intent == null)
            throw new ArgumentNullException(nameof(intent));

        _intents[enemy] = intent;
    }

    // 턴 종료, 웨이브 전환 시 전부 비움. 플래그도 함께 초기화
    // 외부의 전체 리셋용으로 유지. BattleFlowSystem은 더 이상 이걸 턴 경계에 안 씀
    // -> ClearTurn/ClearRevealed로 분리
    public void ClearAll()
    { 
        _intents.Clear();
        _revealed.Clear();
        _cancelled.Clear();
    }

    // 턴 경계 초기화: intent/붕괴취소만. revealed는 유지(정보확인은 웨이브 단위로 지속)
    public void ClearTurn()
    {
        _intents.Clear();
        _cancelled.Clear();
    }

    // 웨이브 경계 초기화: revealed만. 새 웨이브 적은 어차피 새 EnemyUnit 인스턴스라
    // 이 호출 없이도 사실상 무관하지만, 다음 웨이브에서 남아있는 아군 관점 정보를
    // 이월시키지 않는다는 정책을 명시적으로 못박기 위해 별도 유지
    public void ClearRevealed()
    {
        _revealed.Clear();
    }

    // === 조회 === //
    // 없으면 null. 대상 상실, 미등록 적 구분은 호출자 몫
    public EnemyIntent GetIntent(EnemyUnit enemy)
        =>_intents.TryGetValue(enemy, out EnemyIntent intent) ? intent : null;

    // === 정보 조회 뷰 === //
    // intent의 읽기 전용 투영 반환. reveal 게이트로 공개 수준 분기
    // 미등록 적은 null. 결정론 순서는 호출자가 슬롯순 유닛으로 순회
    // 게이트 판정을 이 한 곳에 집중: 무엇이 언제 보이는가 단일 소유
    public IntentView GetView(EnemyUnit enemy)
    { 
        EnemyIntent intent = GetIntent(enemy);
        if (intent == null)
            return null;

        SkillData skill = intent.Skill;
        if (IsRevealed(enemy))
            return IntentView.Full(
                intent.Target, skill.Direction,
                skill.DisplayName, skill.Effects, skill.IsUnavoidable);

        return IntentView.Basic(intent.Target, skill.Direction);
        {
            
        }
    }

    // === 정보확인 플래그 === //
    public bool IsRevealed(EnemyUnit enemy) => _revealed.Contains(enemy);
    // 정보확인(Reveal) 실행 시 켬. 데이터는 안 바뀌고 공개 수준만 오름
    public void SetRevealed(EnemyUnit enemy) => _revealed.Add(enemy);

    // === 공격자 역참조 조회 === //
    // 이 아군을 노리는, 살아있고 정보확인된 적 목록. 방어위상 예상피해 전용
    // 미확인 적은 제외 -> 정보확인이 대응을 실질적으로 바꾼다는 정체성 반영
    // 순서 = enemies 인자 순
    public IReadOnlyList<EnemyUnit> GetAttackers(AllyUnit ally, IReadOnlyList<EnemyUnit> enemies)
    {
        if (ally == null)
            throw new ArgumentNullException(nameof(ally));
        if (enemies == null)
            throw new ArgumentNullException(nameof(enemies));

        var result = new List<EnemyUnit>();
        foreach (EnemyUnit enemy in enemies)
        {
            if (enemy.IsIncapacitated)
                continue;
            if (!IsRevealed(enemy))
                continue;

            EnemyIntent intent = GetIntent(enemy);
            if (intent == null || intent.Target == null)
                continue;   // 미등록 또는 대상 미확정

            // Area는 대표자와 같은 진영 전원이 맞음. 적 스킬의 대표 대상은 항상 아군 특이라
            // -> 대표자가 AllyUnit이면 어느 아군이든 포함
            // Single/FixedTarget/Self는 대상이 정확히 이 아군일 때만
            bool hits = intent.Skill.TargetRule == TargetRule.Area
                ? intent.Target is AllyUnit
                : ReferenceEquals(intent.Target, ally);

            if (hits)
                result.Add(enemy);
        }

        return result;
    }

    // === 붕괴 행동취소 === //
    // intent 제거가 아닌 무효 표시. UI 취소 연출이 원본 intent를 읽어야 하므로 보존
    public void Cancel(EnemyUnit enemy)
    { 
        if(enemy == null)
            throw new ArgumentNullException(nameof(enemy));
        _cancelled.Add(enemy);
    }

    public bool IsCancelled(EnemyUnit enemy) => _cancelled.Contains(enemy);
}