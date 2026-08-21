using System;
using System.Collections.Generic;

// 보호 관계 보관. 보호자 <-> 피보호자. 관계형이라 유닛에 못 붙임
// 상태 있음 -> 인스턴스 클래스. BattleFlowSystem이 소유, ActionResolver에 주입
// 저장방향: 피보호자 -> 보호자. GetProtector(피보호자) 조회에 최적
public sealed class ProtectionSystem
{ 
    // 키 = 피보호자, 값 = 보호자. 한 피보호자당 보호자 1명. 마지막 지정 우선
    private readonly Dictionary<BattleUnit, BattleUnit> _protectorOf = new Dictionary<BattleUnit, BattleUnit>();

    // 6단계 방어대응에서 호출. 보호자가 피보호자를 보호 선언
    // 같은 피보호자 재지정 시 덮어씀. 자기보호 금지
    public void SetProtect(BattleUnit protector, BattleUnit protectee)
    { 
        if(protector == null)
            throw new ArgumentNullException(nameof(protector));
        if(protectee == null)
            throw new ArgumentNullException(nameof(protectee));
        if(ReferenceEquals(protector, protectee))
            throw new ArgumentException("자기 자신은 보호할 수 없음", nameof(protectee));

        _protectorOf[protectee] = protector;
    }

    // 대상 파이프 3스텝에서 호출. 피보호자의 보호자 반환. 없으면 null
    // 반환된 보호자의 생존 여부는 호출자인 TargetingSystem이 판정
    public BattleUnit GetProtector(BattleUnit protectee)
        => (protectee != null && _protectorOf.TryGetValue(protectee, out BattleUnit protector)) ? protector : null;

    // 8단계 턴종료, 웨이브 전환 시 전부 비움. 보호는 한 턴 한정
    public void ClearAll() => _protectorOf.Clear();
}