// 방어자 자세. 방향 판정 입력. 방어방향 + 약점방향 + 불일치 배율을 한 묶음으로
// 불일치 배율을 자세에 넣는 이뉴는 아군 방향방어(능동) 불일치 0.75 보상,
// 적 자세(패시브) 불일치 1.00 무보상 -> 값을 자세가 들어야 판정 함수가 아군/적 타입 분기 없이 읽기만 함
// 여기선 타입 정의만, Data층으로 AttackDirection만 의존
public readonly struct DefenseStance
{ 
    public AttackDirection Defense { get; }     // 방어 자세 방향. None = 방어 자세 없음
    public AttackDirection Weakness { get; }    // 약점 방향. None = 약점 없음. 아군은 항상 None
    public bool IsActive { get; }               // 능동(아군) = true / 패시브(적) = false. 불일치 배율 선택용

    public DefenseStance(AttackDirection defense, AttackDirection weakness, bool isActive)
    { 
        Defense = defense;
        Weakness = weakness;
        IsActive = isActive;
    }

    // 자세 없음
    public static DefenseStance None => new DefenseStance(AttackDirection.None, AttackDirection.None, false);
}