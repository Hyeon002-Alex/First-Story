using System;
using System.Collections.Generic;

// 아군 한 명이 지금 고를 수 있는 선택지 전체
// ChoiceQuerySystem이 산출, UI/드라이버가 소비. 순수 읽기 전용 결과 묶음
public sealed class AllyChoices
{
    public AllyUnit Ally { get; }
    public InputPhase Phase { get; }
    public IReadOnlyList<ActionChoice> Choices { get; }   // 항상 최소 1개(차례종료=포기 공통 포함)

    public AllyChoices(AllyUnit ally, InputPhase phase, IReadOnlyList<ActionChoice> choices)
    {
        Ally = ally ?? throw new ArgumentNullException(nameof(ally));
        Phase = phase;
        Choices = choices ?? throw new ArgumentNullException(nameof(choices));
    }
}