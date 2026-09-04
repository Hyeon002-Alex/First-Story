using System;
using System.Collections.Generic;
using System.Linq;

// 한 InputRequest를 read layer 조회로 풀어 AllyInputPresenter 1개로 조립하고
// -> 확정된 ActionCommand를 회수하는 조정자
// BattleFlowSytem이 위상마다 아군별로 InputRequest를 내밀면, 드라이버가 요청 1건당
// -> Begin -> (풀링) -> TryComplete
public sealed class AllyInputCoordinator
{
    private readonly IReadOnlyList<AllyUnit> _allies;
    private readonly IReadOnlyList<EnemyUnit> _activeEnemies;   // 현재 웨이브. WaveSystem이 내용 교체
    private readonly IntentSystem _intents;                     // null 혀용 -> 예상피해/의도 없이 동작

    // 진행중인 요청 1건의 Presenter. Begin에서 채우고 TryComplete 성공 시 비움
    public AllyInputPresenter CurrentPresenter { get; private set; }
    public InputPhase CurrentPhase { get; private set; }

    public AllyInputCoordinator(
        IReadOnlyList<AllyUnit> allies, 
        IReadOnlyList<EnemyUnit> activeEnemies, 
        IntentSystem intents)
    {
        _allies = allies ?? throw new ArgumentNullException(nameof(allies));
        _activeEnemies = activeEnemies ?? throw new ArgumentNullException(nameof(activeEnemies));
        _intents = intents; // null 허용: 조회 전용 노출이 없거나 헤드리스 조립 시
    }

    // 요청 1건 착수. read layer 조회 -> Presenter 조립. 이미 진행중이면 드라이버 버그. 예외
    // turnNum: 스냅샷용. BattleFlowSystem.TurnNum을 드라이버가 그대로 넘김
    public void Begin(InputRequest request, int turnNum)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (CurrentPresenter == null)
            throw new ArgumentException("이전 요청이 아직 확정되지 않음. TryComplete로 회수 후 Begin");

        // 생존 필터 스냅샷. GetChoices는 재필터 안 하는 계약이라 여기서 걸러 넘김
        BattleSnapshot snapshot = new BattleSnapshot(
            turnNum,
            _allies.Where(a => !a.IsIncapacitated).ToList(),
            _activeEnemies.Where(e => !e.IsIncapacitated).ToList());

        // intents를 위상 무관 항상 넘김: Defense에서 누락 시 방어위상 예상피해가 무증상 손실
        AllyChoices choices = ChoiceQuerySystem.GetChoices(request.DecidingUnit, request.Phase, snapshot, _intents);

        CurrentPresenter = new AllyInputPresenter(choices);
        CurrentPhase = request.Phase;
    }

    // 확정 회수. Presenter가 Commited면 명령을 꺼내고 진행 상태를 비움. 아니면 false. 드라이버는 계속 풀링
    public bool TryComplete(out ActionCommand command)
    {
        if (CurrentPresenter != null && CurrentPresenter.HasResult)
        {
            command = CurrentPresenter.Result;
            CurrentPresenter = null;
            return true;
        }
        command = null;
        return false;
    }

    // 적 의도 표시용 VM. _activeEnemies 슬롯순. intent 미등록/미주입 적은 스킵
    public IReadOnlyList<IntentDisplayVM> EnemyIntents()
    { 
        var list = new List<IntentDisplayVM>();
        if (_intents == null)
            return list;

        foreach (EnemyUnit enemy in _activeEnemies)
        { 
            IntentDisplayVM vm = IntentDisplayMapper.ToVM(_intents.GetView(enemy));
            if (vm != null)
                list.Add(vm);
        }

        return list;
    }
}