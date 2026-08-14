using System.Collections.Generic;
using UnityEngine;

// 상태이상 정의. 정적, 읽기 전용. SkillData.Effects가 직접 참조
// 자기 statusId는 세이브, 로그, 중첩 조회 키로 유지
[CreateAssetMenu(fileName = "Status_", menuName = "Laplace/Status Effect Data")]
public class StatusEffectData : ScriptableObject
{
    [SerializeField] private string _statusId;
    [SerializeField] private string _displayName;
    [SerializeField] private StatusEffectCategory _category;
    [SerializeField] private int _baseDuration;     // 지속 턴
    [SerializeField] List<EffectComponent> _components = new List<EffectComponent>();

    public string StatusId => _statusId;
    public string DisplayName => _displayName;
    public StatusEffectCategory Category => _category;
    public int BaseDuration => _baseDuration;
    public IReadOnlyList<EffectComponent> Components => _components;    // 밖에서 Add/Remove 불가
}
