using System.Collections.Generic;

// 한 턴 결정 시점의 전장 스냅샷. 불변. EnemyBattleSystem이 조립해 Decide로 넘김
// Core인 AllyUnit/EnemyUnit 참조
// 목록은 필터링한 생존을 담음. 생성 측 책임. 소비 측인 Decide는 재필터 안 함
public readonly struct BattleSnapshot
{ 
    public int TurnNum { get; }
    // 플레이어측 유닛. 아군. 적 AI의 공격 대상 후보
    public IReadOnlyList<AllyUnit> LivingAllies { get; }
    // 적측 유닛. 결정 뉴싱이 속한 진영. SurvivingAllyAtLeast 조건이 이 수를 봄
    public IReadOnlyList<EnemyUnit> LivingEnemies { get; }

    public BattleSnapshot(int turnNum, IReadOnlyList<AllyUnit> livingAllies, IReadOnlyList<EnemyUnit> livingEnemies)
    { 
        TurnNum = turnNum;
        LivingAllies = livingAllies;
        LivingEnemies = livingEnemies;
    }
}