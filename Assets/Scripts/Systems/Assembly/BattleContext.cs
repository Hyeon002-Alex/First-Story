using System;
using System.Collections.Generic;

// 조립부(BattleBootstrapper)가 반환하는 런타임 핸들 묶음
// 전투를 구동하고 상태를 조회하는 데 필요한 참조만 노출
public sealed class BattleContext
{
    private readonly WaveSystem _waves;     // 조회 위임용 내부 보관. AdvanceToNextWave 등 뮤테이터는 감춤

    public BattleContext(
        BattleFlowSystem flow,
        IReadOnlyList<AllyUnit> allies,
        IReadOnlyList<EnemyUnit> activeEnemies,
        WaveSystem waves)
    { 
        Flow = flow ?? throw new ArgumentNullException(nameof(flow));
        Allies = allies ?? throw new ArgumentNullException(nameof(allies));
        ActiveEnemies = activeEnemies ?? throw new ArgumentNullException(nameof(activeEnemies));
        _waves = waves ?? throw new ArgumentNullException(nameof(waves));
    }

    public BattleFlowSystem Flow { get; }
    public IReadOnlyList<AllyUnit> Allies { get; }
    public IReadOnlyList<EnemyUnit> ActiveEnemies { get; }   // 현재 웨이브. WaveSystem이 내용 교체하는 공유 리스트의 읽기 뷰
    public int CurrentWaveIndex => _waves.CurrentWaveIndex;  // 조회 전용 위임. WaveSystem 자체는 비노출
    public bool HasNextWave => _waves.HasNextWave;
}