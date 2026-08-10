using System;
using UnityEngine;

// 기본 스탯 4종. 정적 불변 값. 유닛 데이터 안에 박혀 인스펙터에 인라인으로 뜸
[Serializable]
public sealed class UnitStats
{
    [SerializeField] private int _maxHP;
    [SerializeField] private int _attack;
    [SerializeField] private int _defense;
    [SerializeField] private int _speed;

    // 읽기 전용
    public int MaxHP => _maxHP;
    public int Attack => _attack;
    public int Defense => _defense;
    public int Speed => _speed;
}