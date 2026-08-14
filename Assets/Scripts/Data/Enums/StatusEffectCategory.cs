// 상태이상 분류. 정화(일반만 제거) 판별용. 전투 상태는 원래 이 목록 밖이나 강화는 편입
public enum StatusEffectCategory
{ 
    Normal,     // 일반: 화상/중독/둔화/방어저하 등 -> 정화 대상
    Special,    // 특수: 붕괴/균열/표식 등 -> 정화 면제
    Buff        // 버프: 방어/속도/AP회복 등가 등 -> 정화 면제
}