using System;

// 피해, 회복, 보호막 순수 계산. 상태 없음. HP 건드리지 않음. UnityEngine 미의존
// 예상=실제의 열쇠: 미리보기도 실제도 이 함수를 똑같이 호출
// 배율(방향, 붕괴, 받는회복)은 밖에서 판정에 숫자로 주입. 계산 함수는 곱하기만
// 예외: 방어보정 100/(100+방어) 는 스탯 직독 산수라 이 안에서 처리
public static class CombatCalculator
{
    // 피해 사슬: 기본피해 * 방어보정 * 방어계수 * 붕괴 -> 소수점 버림, 최소 1
    // directionMod, breakMod = 밖에서 판정한 배율. 보정 없으면 1.0f 주입
    public static DamageResult CalcDamage(
        int attack, float damageCoeffi, int fixedDamage,
        int defense, float directionMod, float breakMod)
    {
        double basePower = attack * (double)damageCoeffi + fixedDamage;         // 공격력, 계수, 고정피해
        double defenseCorrection = 100.0 / (100.0 + defense);                   // 방어보정
        double raw = basePower * defenseCorrection * directionMod * breakMod;   // 방향, 붕괴
        int finalDamage = Math.Max(1, FloorToInt(raw));                         // 버림, 최소 1

        return new DamageResult(finalDamage, directionMod, breakMod);
    }

    // 회복: 방어, 방향, 붕괴 무보정. 받는회복량 보정만 밖에서 주임
    public static int CalcHealing(int attack, float healingCoeffi, int fixedHealing, float receivedHealingMod)
    { 
        double raw = (attack * (double)healingCoeffi + fixedHealing) * receivedHealingMod;
        return Math.Max(0, FloorToInt(raw));
    }

    // 보호막: 모든 보정 없음. 순수 계수 계산
    public static int CalcShield(int attack, float shiedlCoeffi, int fixedShield)
    { 
        double raw = attack * (double)shiedlCoeffi + fixedShield;
        return Math.Max(0, FloorToInt(raw));
    }

    // 소수점 버림
    private static int FloorToInt(double value) => (int)Math.Floor(value);
}