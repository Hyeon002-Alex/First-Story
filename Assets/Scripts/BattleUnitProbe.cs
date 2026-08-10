using UnityEngine;

// A 대주제 검증용. 커밋 대상 아님(검증 후 삭제/이동).
public class BattleUnitProbe : MonoBehaviour
{
    [SerializeField] private AllyUnitData _allyData;
    [SerializeField] private EnemyUnitData _enemyData;
    [SerializeField] private SkillData _sampleSkill;

    private void Start()
    {
        var ally = new AllyUnit(_allyData);
        var enemy = new EnemyUnit(_enemyData);

        // 1) 생성 직후
        Debug.Log($"[아군 {ally.UnitId}] HP {ally.CurrHP}/{ally.MaxHP} AP {ally.CurrAP} " +
                  $"유효 공/방/속 {ally.EffectiveAttack}/{ally.EffectiveDefense}/{ally.EffectiveSpeed} " +
                  $"전투불능 {ally.IsIncapacitated}");

        // 2) HP 2층 확인 — 런타임층(CurrentHp)만 움직이고 정적층(MaxHp) 고정 + 클램프
        ally.ModifyHP(-9999);
        Debug.Log($"[아군] 과다 피해 후 HP {ally.CurrHP}/{ally.MaxHP} (0 클램프)");
        ally.ModifyHP(9999);
        Debug.Log($"[아군] 과다 회복 후 HP {ally.CurrHP}/{ally.MaxHP} (maxHp 클램프)");

        // 3) 통로 동작
        ally.SetShield(15);
        ally.SetEvasion(2);   // 상한 3은 EvasionSystem 몫 — 통로는 음수만 방지
        ally.SetAP(4);
        Debug.Log($"[아군] 보호막 {ally.Shield} 회피 {ally.EvasionCount} AP {ally.CurrAP}");

        // 4) 전투불능 플래그(전이 판단은 F-1, 여기선 자리만)
        ally.SetIncapacitated(true);
        Debug.Log($"[아군] 전투불능 세팅 후 {ally.IsIncapacitated}");

        // 5) 적 게이지·보스 구분
        enemy.SetGauge(3);
        Debug.Log($"[적 {enemy.EnemyId}] HP {enemy.CurrHP}/{enemy.MaxHP} 게이지 {enemy.CurrBreakOrCrackGauge} 보스 {enemy.IsBoss}");

        // 6) SkillData 참조 로그
        if (_sampleSkill != null)
            Debug.Log($"[스킬 {_sampleSkill.SkillId}] AP {_sampleSkill.ApCost} 피해계수 {_sampleSkill.DamageCoefficient} " +
                      $"방향 {_sampleSkill.Direction} 대상 {_sampleSkill.TargetRule} 회피불가 {_sampleSkill.IsUnavoidable}");
    }
}