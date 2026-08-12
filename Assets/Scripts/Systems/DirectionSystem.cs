// 공격 방향 vs 방어자 자세 -> 최종 피해 배율. 순수 판정, 상태 없음, 아군/적 대칭
// 배율은 밖으로 넘겨 CombatCalculator가 곱함
// 배율 값 = 여기서. BattleConfig가 생기면 이관
public static class DirectionSystem
{
    // === 배율 테이블(프로토타입 값. 데이터 이관 대기) === //
    public static readonly float NoneMod = 1.00f;           // 방향 없는 공격 / 자세 없음
    public static readonly float MatchMod = 0.30f;          // 방향 일치 방어. 강한 감소
    public static readonly float WeaknessMod = 1.25f;       // 방향 약점 피격
    public static readonly float AllyMismatchMod = 0.75f;   // 아군 방향방어 불일치. 능동 보상
    public static readonly float EnemyMismatchMod = 1.00f;  // 적 자세 불일치. 패시브 무보상

    // 공격 방향 + 방어자 자세 -> 배율
    // 방향없음 -> 약점 -> 일치 -> 불일치 -> 자세없음
    public static float GetMod(AttackDirection attackDir, DefenseStance stance)
    {
        // 1. 방향 없는 공격(회복/보호/정보 등) = 배율 없음
        if (attackDir == AttackDirection.None)
            return NoneMod;

        // 2. 약점 우선(방어+약점 동시일 때 약점이 우선)
        if (stance.Weakness != AttackDirection.None && attackDir == stance.Weakness)
            return WeaknessMod;

        // 3. 방어 일치
        if (stance.Defense != AttackDirection.None && attackDir == stance.Defense)
            return MatchMod;

        // 4. 방어는 있는데 불일치 -> 자세가 든 배율(아군 0.75, 적 1.00)
        if (stance.Defense != AttackDirection.None)
            return stance.MismathMod;

        // 5. 자세 없음
        return NoneMod;
    }
}