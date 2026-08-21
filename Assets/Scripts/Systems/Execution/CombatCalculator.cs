using System;
using UnityEngine.UIElements;

// 피해, 회복, 보호막 순수 계산. 상태 없음. HP 건드리지 않음. UnityEngine 미의존
// 예상=실제의 열쇠: 미리보기도 실제도 이 함수를 똑같이 호출
// [정수 스케일 계산] 계수/배율(float)을 진입점에서 스케일 정수로 반올림(1.3f -> 130)
// 반올림이 float 표현오차를 흡수 -> 이후 정수 산출 -> 오차 없음
// Scale = 배율/계수 유효 소수자리(2자리)
public static class CombatCalculator
{
    private const int _scale = 100;

    // 피해 사슬: 기본피해 * 방어보정 * 방어계수 * 붕괴 * 받는피해증가 -> 소수점 버림, 최소 1
    // directionMod, breakMod, receivedDamageMod = 밖에서 판정한 배율. 보정 없으면 1.0f 주입
    public static DamageResult CalcDamage(
        int attack, float damageCoeffi, int fixedDamage,
        int defense, float directionMod, float breakMod, float receivedDamageMod)
    {
        long baseScaled = (long)attack * ToScaled(damageCoeffi) + (long)fixedDamage * _scale;
        long dir = ToScaled(directionMod);
        long brk = ToScaled(breakMod);
        long rcv = ToScaled(receivedDamageMod);

        // 최종 = baseScaled / _s * 100/(100+def) * dir/_s * brk/_s * rcv/_s
        long numerator = baseScaled * 100L * dir * brk * rcv;
        long denominator = (long)(100 + defense) * _scale * _scale * _scale * _scale;
        int finalDamage = (int)Math.Max(1, numerator / denominator);   // 양수 정수나눗셈 = floor

        return new DamageResult(finalDamage, directionMod, breakMod, receivedDamageMod);
    }

    // 회복: 방어, 방향, 붕괴 무보정. 받는회복량 보정만 밖에서 주임
    public static int CalcHealing(int attack, float healingCoeffi, int fixedHealing, float receivedHealingMod)
    {
        long baseScaled = (long)attack * ToScaled(healingCoeffi) + (long)fixedHealing * _scale;
        long rcv = ToScaled(receivedHealingMod);
        long numerator = baseScaled * rcv;
        long denominator = (long)_scale * _scale;
        return (int)(numerator / denominator);
    }

    // 보호막: 모든 보정 없음. 순수 계수 계산
    public static int CalcShield(int attack, float shiedlCoeffi, int fixedShield)
    { 
        long baseScaled = (long)attack * ToScaled(shiedlCoeffi) + (long)fixedShield * _scale;
        return (int)Math.Max(0, baseScaled / _scale);
    }

    // 

    // 배율/방어 없는 순수 계수 곱
    public static int CalcFlat(int attack, float coeffi)
    { 
        long baseScaled = (long)attack * ToScaled(coeffi);
        return (int)Math.Max(0, baseScaled / _scale);
    }

    // float 계수/배율 -> 스케일 정수. 반올림이 표현오차 흡수
    private static long ToScaled(float value) => (long)Math.Round(value * _scale, MidpointRounding.AwayFromZero);
}