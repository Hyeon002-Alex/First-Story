using System;
using UnityEngine;

// 효과 조각. 순수 수치 기술, 자체 로직 없음. 소비 시스템이 effectKind로 필터
// 조각 1개 = 단일 효과, 2개 이상 = 복합 효과
[Serializable]
public struct EffectComponent
{
    [SerializeField] private EffectKind _effectKind;
    [SerializeField] private float _magnitude;  // 계수/감소량/배율 혼용 -> float 통일. effectKind가 의미 결정

    public EffectKind EffectKind => _effectKind;
    public float Magnitude => _magnitude;
}