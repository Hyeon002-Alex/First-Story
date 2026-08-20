using System.Collections.Generic;

// 조립부(BattleBootstrapper)가 반환하는 런타임 핸들 묶음
// 전투를 구동하고 상태를 조회하는 데 필요한 참조만 노출
public sealed class BattleContext
{
    public BattleContext(
        BattleFlowSystem flow,
        IReadOnlyList<AllyUnit> allies,
        IReadOnlyList<EnemyUnit> activeEnemies,
        WaveSystem waves,
        IntentSystem intents)
    { 
        Flow = flow;
        Allies = allies;
        ActiveEnemies = activeEnemies;
        Waves = waves;
        Intents = intents;
    }

    public BattleFlowSystem Flow { get; }
    public IReadOnlyList<AllyUnit> Allies { get; }
    public IReadOnlyList<EnemyUnit> ActiveEnemies { get; }   // 현재 웨이브. WaveSystem이 내용 교체하는 공유 리스트의 읽기 뷰
    public WaveSystem Waves { get; }
    public IntentSystem Intents { get; }
}