using System;

// HP0 도달 -> 전투불능 규칙 단일 소유. 상태 없음
// 파이프 사망판정 + 8단계 틱 후 전이가 전부 이 통로로 수렴
public static class IncapacitationSystem
{
    // HP0 && 아직 전투불능 아님 -> 전투불능 set
    // 반환: 이번 호출로 새로 전이됐으면 true. 로그/후처리는 호출측
    // 아군/적 공통. HP1로 복귀는 아군 전용이라 WaveSystem이 소유
    public static bool CheckAndTransition(BattleUnit unit)
    { 
        if(unit == null)
            throw new ArgumentNullException(nameof(unit));
        if (unit.CurrHP == 0 && !unit.IsIncapacitated)
        { 
            unit.SetIncapacitated(true);
            return true;
        }
        return false;
    }
}