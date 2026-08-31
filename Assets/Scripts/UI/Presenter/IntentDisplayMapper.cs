using System.Linq;

// IntentView -> IntentDisplayVM 순수 변환
// 적 의도 표시는 클릭에 반응하는 선택 상대가 없는 순수 표시
// -> 클릭 상태를 갖는 AllyInputPresenter 상태머신과 분리해 응집도를 지킴
public static class IntentDisplayMapper
{
    public static IntentDisplayVM ToVM(IntentView view)
    {
        if (view == null)
            return null;    // 호출측 계약: null = intent 없는 적

        return new IntentDisplayVM(
            view.Target,
            UnitLabel.Of(view.Target),
            view.Direction,
            view.IsRevealed,
            view.DisplayName,
            view.Effects.Select(e => e.DisplayName).ToList(),
            view.IsUnavoidable);
    }
}