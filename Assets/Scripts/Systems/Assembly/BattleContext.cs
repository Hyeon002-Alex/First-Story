using System;
using System.Collections;
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

    // === 헤드리스/테스트 전용 동기 구동 === //
    // 코루틴 펌프를 커밋 API로 노출. 전투를 승패까지 완주시키고 결과 반환
    // UI: 이 메서드를 쓰지 않고 context.Flow.ExecuteTurn()을 StartCoroutine으로 비동기 펌프
    // InputRequest는 IPlayerInputSource가 동기 결정 -> SetResponse로 흐름에 되돌림
    public BattleOutcome RunBattle(IPlayerInputSource inputSource)
    {
        if (inputSource == null)
            throw new ArgumentNullException(nameof(inputSource));

        // 무한루프 방어: 유한 턴에 종료해야 함. 초과 시 설계 결함
        const int turnGuard = 1000;
        int turns = 0;

        do
        {
            IEnumerator turn = Flow.ExecuteTurn();
            while (turn.MoveNext())
            {
                // 입력 필요 지점: 흐름이 내민 요청을 소스가 즉시 해결 -> 슬롯에 되돌림
                if (turn.Current is InputRequest req)
                    req.SetResponse(inputSource.Resolve(req));
            }

            if (++turns > turnGuard)
                throw new InvalidOperationException(
                    $"[{nameof(RunBattle)}] 턴 {turnGuard} 초과 미종료 - 종료 판정 결함 의심");
        }
        while (Flow.LastOutcome == BattleOutcome.Ongoing);

        return Flow.LastOutcome;
    }
}