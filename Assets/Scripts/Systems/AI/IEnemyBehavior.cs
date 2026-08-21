// 적 하나의 아본 톤 결정 계약. 일반몹, 보스가 공통 구현
// -> 호출부(EnemyBehaviorSystem)는 일반몹/보스를 구별 못 함
public interface IEnemyBehavior
{
    // 대상 후보 없음 등으로 결정 불가 시 null 반환 허용. 호출부가 미등록 철
    EnemyIntent Decide(BattleSnapshot snapshotm, EnemyUnit self);
}