using System;
using System.Collections.Generic;
using UnityEngine;

// 한 전투의 정적 정의. 조립부(BattleBootstraper)가 이걸 읽어 런타임 전투를 구성
// 담는 것: 기본 파티 + 웨이브별 등장 적 + 등장 적이 쓰는 행동 패턴
// 밸런스 값은 참조된 각 SO가 소유
[CreateAssetMenu(fileName = "Encounter_", menuName = "Laplace/Encounter Data")]
public sealed class EncounterData : ScriptableObject
{
    // 웨이브 1개를 감싸는 최소 래퍼. Unity는 List<List<T>>를 직렬화하지 못함
    // EncounterData 전용
    [Serializable]
    public sealed class Wave
    {
        [SerializeField] private List<EnemyUnitData> _enemies = new List<EnemyUnitData>();
        public IReadOnlyList<EnemyUnitData> Enemies => _enemies;
    }

    [SerializeField] private string _encounterId;

    // 이 인카운터의 기본 파티. 편성/성장 도입 시 조립부가 그쪽 파티로 대체하고
    // 이 필드는 프리뷰/폴백으로 남음
    [SerializeField] private List<AllyUnitData> _defaultParty = new List<AllyUnitData>();

    // 웨이브 순서 = 리스트 순서. 각 Wave = 그 웨이브에 등장하는 적 목록
    [SerializeField] private List<Wave> _waves = new List<Wave>();

    // 이 인카운터 등장 적들이 참조하는 행동 패턴 집합
    // 조립부가 여기서 patternId -> BehaviorPattern 레지스트리를 만듦
    // 등장 적의 BehaviorPatternId는 전부 이 목록의 어느 PatternId와 일치해야 함
    [SerializeField] private List<BehaviorPatternData> _patterns = new List<BehaviorPatternData>();

    public string EncounterId => _encounterId;
    public IReadOnlyList<AllyUnitData> DefaultParty => _defaultParty;
    public IReadOnlyList<Wave> Waves => _waves;
    public IReadOnlyList<BehaviorPatternData> Patterns => _patterns;
}
