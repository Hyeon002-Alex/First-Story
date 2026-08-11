using System.Collections.Generic;

// 적 intent 일괄 보관, 공개. 상태 있음 -> 인스턴스 클래스. APSystem과 반대
// 유닛에 intent를 실음: 대응단계, UI가 살아있는 모든 적 intent를 한 곳에서 순회하려고
public sealed class IntentSystem
{ 
    // 적별 의도. 참조 키로 조회
    private readonly Dictionary<EnemyUnit, EnemyIntent> _intents = new Dictionary<EnemyUnit, EnemyIntent>();

    // 정보 확인 플래그: intent와 분리 소유. intent는 불변으로 유지
    private readonly HashSet<EnemyUnit> _revealed = new HashSet<EnemyUnit>();

    // === 등록, 소거 === //
    // 적 intent 등록. 같은 적 재등록 시 덮어씀
    public void SetIntent(EnemyUnit enemy, EnemyIntent intent)
    {
        _intents[enemy] = intent;
    }

    // 턴 종료, 웨이브 전환 시 전부 비움. 플래그도 함께 초기화
    public void ClearAll()
    { 
        _intents.Clear();
        _revealed.Clear();
    }

    // === 조회 === //
    // 없으면 null. 대상 상실, 미등록 적 구분은 호출자 몫
    public EnemyIntent GetIntent(EnemyUnit enemy)
        =>_intents.TryGetValue(enemy, out EnemyIntent intent) ? intent : null;

    // 대응단계, UI가 살아있는 모든 적 intent를 훑는 통로. 읽기 전용 노출
    public IReadOnlyDictionary<EnemyUnit, EnemyIntent> AllIntents => _intents;

    // === 정보확인 플래그 === //
    public bool IsRevealed(EnemyUnit enemy) => _revealed.Contains(enemy);
    // 정보확인(Reveal) 실행 시 켬. 데이터는 안 바뀌고 공개 수준만 오름
    public void SetRevealed(EnemyUnit enemy) => _revealed.Add(enemy);
}