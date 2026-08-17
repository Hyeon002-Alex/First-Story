using System;
using System.Collections.Generic;
using UnityEngine;

// 웨이브 전환 시퀀스 소유. 순서만 조율. 각 소거는 담당 시스템 통로 호출
// 활성 적 리스틑 BattleFlow와 같은 인스턴스 고융
public sealed class WaveSystem
{
    private readonly List<EnemyUnit> _activeEnemies;                    // BattleFlow와 공유
    private readonly IReadOnlyList<IReadOnlyList<EnemyUnit>> _waves;    // 웨이브별 적. 
    private readonly IReadOnlyList<AllyUnit> _allies;
    private readonly ProtectionSystem _protection;
    private int _currentWaveIndex;

    // _waves는 후속 SO가 대체 예정. 지금은 셋업이 직접 주입
    public WaveSystem(
        List<EnemyUnit> activeEnemies,
        IReadOnlyList<IReadOnlyList<EnemyUnit>> waves,
        IReadOnlyList<AllyUnit> allies,
        ProtectionSystem protection)
    { 
        _activeEnemies = activeEnemies ?? throw new ArgumentNullException(nameof(activeEnemies));
        _waves = waves ?? throw new ArgumentNullException(nameof(waves));
        _allies = allies ?? throw new ArgumentNullException(nameof(allies));
        _protection = protection ?? throw new ArgumentNullException(nameof(_protection));
        _currentWaveIndex = 0;
    }

    public int CurrentWaveIndex => _currentWaveIndex;
    public bool HasNextWave => _currentWaveIndex + 1 < _waves.Count;

    // 다음 웨이브로 전환. 전환순서 5~9 실행
    public void AdvanceToNextWave()
    {
        if (!HasNextWave)
            throw new InvalidOperationException("다음 웨이브 없음. 호출 전 HAsNextWave 확인 필요");

        // 5. 전투상태 강제소거. 전원 = 아군. 적은 곧 교체라 제외
        ShieldSystem.ClearAll(_allies);
        EvasionSystem.ClearAll(_allies);
        _protection.ClearAll();
        StatusEffectSystem.ClearBuff(_allies);
        foreach (AllyUnit a in _allies)
        {
            a.ClearStance();    // 방향방어. 한 턴 한정이라 이미 소거됐을 수 있으나 진행
        }

        // 6. 전투불능 아군 HP1 복귀 + 7. 북귀유닛 일반상태 제거
        // AP는 직전 값유지
        foreach (AllyUnit a in _allies)
        { 
            if (a.IsIncapacitated)
            {
                a.ModifyHP(1);                      // 전투불능 -> HP0 불변식 -> 0+1 = 1
                a.SetIncapacitated(false);
                StatusEffectSystem.ClearNormal(a);  // 복귀유닛만. 생존유닛 일반상태는 유지
                Debug.Log($"[웨이브 복귀] {a.UnitId} HP1, AP {a.CurrAP} 유지");
            }
        }

        // 8. 생존유닛 HP, AP, 일반상태 유지 = 아무것도 안 함

        // 9. 다음 웨이브 적 생성: 활성 리스트 내용 교체. 같은 인스턴스인 BattleFlow/ActionResolver 참조 전파
        _currentWaveIndex++;
        _activeEnemies.Clear();
        foreach (EnemyUnit e in _waves[_currentWaveIndex])
        { 
            _activeEnemies.Add(e);
        }

        Debug.Log($"[웨이브 전환] {_currentWaveIndex}번째 웨이브. 적 {_activeEnemies.Count}명");
    }
}