// 단일 대상 스킬에서 누구를 노리는지. targetRule이 형태를 가르고, 이건 단일일 때의 답
// 선택 로직은 Systems 소유
// 같을 경우 파티 슬롯 인덱스 오름차순 고정
public enum TargetPolicy
{ 
    FirstAlive,     // 슬롯 앞 첫 유닛. 가장 단순한 기본값
    LowestHP,       // 현재 Hp 최소. 마무리
    HighestHP,      // 현재 HP 최대. 탱커 압박
    HighestAttack   // 최대 공격력. 딜러 제거
}