using System;
using System.Collections.Generic;
using UnityEngine;

// EncounterData를 읽어 런타임 전투 한 판을 조립
// 유닛 생성 -> 패턴 레지스트리/웨이브 목록 구성 -> 시스템 배선 -> BattleContext 반환
// 순수 C#
public static class BattleBootstrapper
{
    // encounter로부터 완성된 전투 핸들을 만듦. 데이터 결함은 배선 전에 예외로 걸러 조기 실패시킴
    public static BattleContext Build(EncounterData encounter)
    { 
        if (encounter == null)
            throw new ArgumentNullException(nameof(encounter));

        // 1. 파티 생성
        IReadOnlyList<AllyUnitData> partyData = encounter.DefaultParty;
        if (partyData == null || partyData.Count == 0)
            throw new InvalidOperationException($"[{encounter.EncounterId}] 파티가 비어 있음");

        List<AllyUnit> allies = new List<AllyUnit>(partyData.Count);
        foreach (AllyUnitData d in partyData)
        {
            if (d == null)
                throw new InvalidOperationException($"[{encounter.EncounterId}] 파티에 null 항목");
            allies.Add(new AllyUnit(d));
        }

        // 2. 패턴 레지스트리
        // patternId 중복은 조립 시점에 잡음 - 런타임 조회 모호성 사전 차단
        Dictionary<string, BehaviorPatternData> registry = new Dictionary<string, BehaviorPatternData>();
        foreach (BehaviorPatternData p in encounter.Patterns)
        {
            if (p == null)
                throw new InvalidOperationException($"[{encounter.EncounterId}] 패턴 목록에 null 항목");
            if(registry.ContainsKey(p.PatternId))
                throw new InvalidOperationException($"[{encounter.EncounterId}] 패턴 ID 중복: {p.PatternId}");
            registry.Add(p.PatternId, p);
        }

        // 3. 웨이브 전체 선인스턴스화. WaveSystem이 모든 웨이브를 미리 받는 계약이라 여기서 전부 생성
        // 생성하며 각 적의 patternId가 레지스트리에 있는지 검즘 -> 런타임 무생동(미등록 패턴) 사전 차단
        IReadOnlyList<EncounterData.Wave> waveDefs = encounter.Waves;
        if (waveDefs == null || waveDefs.Count == 0)
            throw new InvalidOperationException($"[{encounter.EncounterId}] 웨이브가 비어 있음");

        List<IReadOnlyList<EnemyUnit>> waves = new List<IReadOnlyList<EnemyUnit>>(waveDefs.Count);
        for (int w = 0; w < waveDefs.Count; w++)
        {
            IReadOnlyList<EnemyUnitData> waveEnemies = waveDefs[w].Enemies;
            if (waveEnemies == null || waveEnemies.Count == 0)
                throw new InvalidOperationException($"[{encounter.EncounterId}] 웨이브 {w}가 비어 있음");

            List<EnemyUnit> built = new List<EnemyUnit>(waveEnemies.Count);
            foreach (EnemyUnitData ed in waveEnemies)
            {
                if (ed == null)
                    throw new InvalidOperationException($"[{encounter.EncounterId}] 웨이브 {w}에 null 적");
                if (!registry.ContainsKey(ed.BehaviorPatternId))
                    throw new InvalidOperationException(
                        $"[{encounter.EncounterId}] 적 {ed.EnemyId}의 패턴 '{ed.BehaviorPatternId}'가 인카운터 패턴 목록에 없음");
                built.Add(new EnemyUnit(ed));
            }
            waves.Add(built);
        }

        // 4. 공유 활성 적 리스트. WaveSystem, ActionResolver, BattleFlowSystem이 같은 인스턴스를 참조해야
        // 웨이브 전환이 세 곳에 전파
        // waves[0]과는 별도의 인스턴스로 둠 -> 전환시 아카이브 훼손 방지
        List<EnemyUnit> activeEnemies = new List<EnemyUnit>(waves[0]);

        // 5. 시스템 배선
        IntentSystem intent = new IntentSystem();
        ProtectionSystem protection = new ProtectionSystem();
        WaveSystem waveSystem = new WaveSystem(activeEnemies, waves, allies, protection);
        IActionExecutor executor = new ActionResolver(allies, activeEnemies, protection, intent);
        EnemyBehaviorSystem behavior = new EnemyBehaviorSystem(registry, intent);
        BattleFlowSystem flow = new BattleFlowSystem(
            allies, activeEnemies, intent, protection, executor, waveSystem, behavior);

        Debug.Log($"[조립 완료] {encounter.EncounterId}: 아군 {allies.Count} / 웨이브 {waves.Count} / 패턴 {registry.Count}");
        return new BattleContext(flow, allies, activeEnemies, waveSystem);
    }
}