// BattleUnit 표시명 통합 접근점. Ally/Enemy가 각자 DisplayName을 갖지만 BattleUnit에는 없어
// -> 패턴매치가 여러 곳에서 반복되는 것을 막으려 여기 하나로 모음
// DisplayName 미입력 시 ID로 대체
public static class UnitLabel
{
    public static string Of(BattleUnit unit)
    {
        if (unit == null)
            return null;
        if (unit is AllyUnit ally)
            return string.IsNullOrEmpty(ally.DisplayName) ? ally.UnitId : ally.DisplayName;
        if (unit is EnemyUnit enemy)
            return string.IsNullOrEmpty(enemy.DisplayName) ? enemy.EnemyId : enemy.DisplayName;

        return unit.GetType().Name;     // 신규 BattleUnit 파생 대비 방어. 도달 시 이 유틸 확장 필요 신호
    }
}