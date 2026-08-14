// 상태이상 효과 조각의 종류. magnitude를 이 값이 해석
public enum EffectKind
{ 
    DamageOverTime,         // 지속피해(화상/중독). magnitude = 계수
    SpeedMod,               // 속도보정
    DefenseMod,             // 방어보정
    APRecoveryMod,          // AP회복보정
    ActionBlock,            // 행동차단. magnitude = 미사용. 존재만 함
    ReceivedHeaingMod,      // 받는회복보정. magnitude = 배율
    ReceivedDamageMod,      // 받는피해보정. magnitude = 배율. 붕괴와 분리된 개념
}