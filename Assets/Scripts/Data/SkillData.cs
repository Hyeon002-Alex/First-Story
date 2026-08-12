using System.Collections.Generic;
using UnityEngine;

// 행동 하나의 정의. 고유행동, 스킬, 아이템 등을 이 한 클래스로 표현
// 유닛은 이걸 직접 안 물고 skillId로 참조
[CreateAssetMenu(fileName = "Skill_", menuName = "Laplace/Skill Data")]
public sealed class SkillData : ScriptableObject
{
    // 식별, 비용
    [SerializeField] private string _skillId;
    [SerializeField] private int _apCost;   // 아이템이면 0

    // 효과 수치 - 계산 입력
    [SerializeField] private float _damageCoeffi;
    [SerializeField] private int _fixedDamage;
    [SerializeField] private float _healingCoeffi;
    [SerializeField] private int _fixedHealing;
    [SerializeField] private float _shieldCoeffi;
    [SerializeField] private int _fixedShield;
    [SerializeField] private int _breakAmount;         // 스킬별 고정 붕괴량. 공격력, 방어, 방향 무관
    [SerializeField] private AttackDirection _direction;

    // 대상
    [SerializeField] private TargetRule _targetRule;

    // 부가 효과
    [SerializeField] private List<string> _effectIds = new List<string>();   // 부여할 상태이상 Id. 순수 참조
    [SerializeField] private bool _cleansesNormalStatus;    // 일반 상태이상 정화 여부. 부여의 반대 동작

    // 정보 확인 속성
    [SerializeField] private bool _isUnavoidable;       // 회피불가

    public string SkillId => _skillId;
    public int ApCost => _apCost;
    public float DamageCoeffi => _damageCoeffi;
    public int FixedDamage => _fixedDamage;
    public float HealingCoeffi => _healingCoeffi;
    public int FixedHealing => _fixedHealing;
    public float ShieldCoeffi => _shieldCoeffi;
    public int FixedShield => _fixedShield;
    public int BreakAmount => _breakAmount;
    public AttackDirection Direction => _direction;
    public TargetRule TargetRule => _targetRule;
    public IReadOnlyList<string> EffectIds => _effectIds;  // 읽기전용 목록 → 밖에서 Add/Remove 못 함
    public bool CleansesNormalStatus => _cleansesNormalStatus;
    public bool IsUnavoidable => _isUnavoidable;
}
