// I 대주제 통합 프로브 (재구성본). 아군 입력 = 요청/응답 코루틴.
// I-1 입력계약 / I-2 ResponsePhaseSystem / I-3 지연실행 / I-4 Step6·7 배선(D5) / I-5 RunBattle 완주(D4).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

public static class IProbe
{
    // I-4 부분조립용 no-op executor. I-4 관심사는 요청 발행 패턴(D5 스킵)이지 실행 로직(C 소관)이 아님
    sealed class NoopExecutor : IActionExecutor
    {
        public void Execute(ActionCommand command, int currentTurn) { }
    }

    static int _pass, _fail;
    static void Check(string name, bool cond)
    {
        if (cond) _pass++;
        else { _fail++; Console.WriteLine("  [FAIL] " + name); }
    }
    static void CheckThrow(string name, Action a)
    {
        try { a(); Check(name, false); } catch { Check(name, true); }
    }

    const BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
    static void Set(object o, string field, object val)
    {
        FieldInfo f = null;
        for (Type t = o.GetType(); t != null && f == null; t = t.BaseType) f = t.GetField(field, BF);
        if (f == null) throw new Exception("필드 없음: " + field);
        f.SetValue(o, val);
    }
    static IEnumerator Pump(object flow, string method)
        => (IEnumerator)typeof(BattleFlowSystem).GetMethod(method, BF).Invoke(flow, null);

    static UnitStats Stats(int hp)
    {
        object box = default(UnitStats);
        Set(box, "_maxHP", hp); Set(box, "_attack", 10); Set(box, "_defense", 10); Set(box, "_speed", 10);
        return (UnitStats)box;
    }
    static SkillData Skill(string id, int ap, int dmg = 0, TargetRule rule = TargetRule.Single)
    {
        var s = new SkillData();
        Set(s, "_skillId", id); Set(s, "_displayName", id);
        Set(s, "_apCost", ap); Set(s, "_isInfoAction", false);
        Set(s, "_direction", AttackDirection.None);
        Set(s, "_fixedDamage", dmg); Set(s, "_targetRule", rule);
        return s;
    }
    static AllyUnit Ally(string id, int ap, bool incap = false)
    {
        var d = new AllyUnitData();
        Set(d, "_unitId", id); Set(d, "_displayName", id); Set(d, "_baseStats", Stats(100));
        var a = new AllyUnit(d); a.SetAP(ap);
        if (incap) a.SetIncapacitated(true);
        return a;
    }
    static EnemyUnit Enemy(string id)
    {
        var d = new EnemyUnitData();
        Set(d, "_enemyId", id); Set(d, "_displayName", id); Set(d, "_baseStats", Stats(100));
        return new EnemyUnit(d);
    }
    // BattleBootstrapper.Build 레시피 재현 (빈 registry → 적 무행동)
    static (BattleContext ctx, BattleFlowSystem flow) Build(List<AllyUnit> allies, List<EnemyUnit> wave0)
    {
        var active = new List<EnemyUnit>(wave0);
        var waves = new List<IReadOnlyList<EnemyUnit>> { wave0 };
        var intent = new IntentSystem();
        var prot = new ProtectionSystem();
        var waveSys = new WaveSystem(active, waves, allies, prot);
        IActionExecutor exec = new ActionResolver(allies, active, prot, intent);
        var behavior = new EnemyBehaviorSystem(new Dictionary<string, BehaviorPatternData>(), intent);
        var flow = new BattleFlowSystem(allies, active, intent, prot, exec, waveSys, behavior);
        return (new BattleContext(flow, allies, active, waveSys), flow);
    }

    public static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        UnityEngine.Debug.Muted = true;

        // ===== I-1: 입력 계약 (10) =====
        CheckThrow("I-1 InputRequest null 주체", () => new InputRequest(InputPhase.Action, null));
        var a = Ally("A", 5);
        var c1 = ActionCommand.CreateEndTurn(a);
        var c2 = ActionCommand.CreateDefense(a, AttackDirection.High);
        var src = new ScriptedInputSource(new[] { c1, c2 });
        var reqA = new InputRequest(InputPhase.Action, a);
        Check("I-1 큐 순서 1", ReferenceEquals(src.Resolve(reqA), c1));
        Check("I-1 큐 순서 2", ReferenceEquals(src.Resolve(reqA), c2));
        var drained = src.Resolve(reqA);
        Check("I-1 소진 EndTurn Kind", drained.Kind == ActionKind.EndTurn);
        Check("I-1 소진 EndTurn Actor==주체", ReferenceEquals(drained.Actor, a));
        CheckThrow("I-1 SetResponse null", () => new InputRequest(InputPhase.Action, a).SetResponse(null));
        var rq = new InputRequest(InputPhase.Action, a);
        rq.SetResponse(ActionCommand.CreateEndTurn(a));
        CheckThrow("I-1 SetResponse 재응답", () => rq.SetResponse(ActionCommand.CreateEndTurn(a)));
        CheckThrow("I-1 SetResponse Actor 불일치",
            () => new InputRequest(InputPhase.Action, a).SetResponse(ActionCommand.CreateEndTurn(Ally("Z", 5))));
        var rc = new InputRequest(InputPhase.Action, a);
        var cc = ActionCommand.CreateEndTurn(a);
        rc.SetResponse(cc);
        Check("I-1 왕복 IsAnswered", rc.IsAnswered);
        Check("I-1 왕복 동일 인스턴스", ReferenceEquals(rc.Response, cc));

        // ===== I-2: ResponsePhaseSystem (13) =====
        var d1 = Ally("D", 5);
        Check("I-2 방향방어 true", ResponsePhaseSystem.TryApply(ActionCommand.CreateDefense(d1, AttackDirection.High), new ProtectionSystem()));
        Check("I-2 방향방어 자세 적용", d1.GetDefenseStance().Defense == AttackDirection.High);
        Check("I-2 방향방어 AP-1", d1.CurrAP == 4);
        Check("I-2 방향방어 acted", d1.ActedThisTurn);

        var p1 = Ally("P1", 5); var p2 = Ally("P2", 5); var prot = new ProtectionSystem();
        Check("I-2 보호 true", ResponsePhaseSystem.TryApply(ActionCommand.CreateProtection(p1, p2), prot));
        Check("I-2 보호자 등록(주입 공유)", ReferenceEquals(prot.GetProtector(p2), p1));
        Check("I-2 보호 AP-1", p1.CurrAP == 4);
        Check("I-2 보호 acted", p1.ActedThisTurn);

        var poor = Ally("Poor", 0);
        Check("I-2 AP부족 false", !ResponsePhaseSystem.TryApply(ActionCommand.CreateDefense(poor, AttackDirection.High), new ProtectionSystem()));
        Check("I-2 AP부족 AP무변", poor.CurrAP == 0);
        Check("I-2 AP부족 자세 미적용", poor.GetDefenseStance().Defense == AttackDirection.None);

        var self = Ally("Self", 5);
        Check("I-2 자기보호 false", !ResponsePhaseSystem.TryApply(ActionCommand.CreateProtection(self, self), new ProtectionSystem()));
        Check("I-2 자기보호 AP무변", self.CurrAP == 5);
        CheckThrow("I-2 EndTurn throw", () => ResponsePhaseSystem.TryApply(ActionCommand.CreateEndTurn(Ally("ET", 5)), new ProtectionSystem()));

        // ===== I-3: 지연 실행 (3) =====
        var (ctx3, flow3) = Build(new List<AllyUnit> { Ally("L", 5) }, new List<EnemyUnit> { Enemy("LE") });
        Check("I-3 펌프전 LastOutcome Ongoing", flow3.LastOutcome == BattleOutcome.Ongoing);
        int t0 = flow3.TurnNum;
        IEnumerator turn = flow3.ExecuteTurn();
        Check("I-3 ExecuteTurn 호출만으론 TurnNum 불변", flow3.TurnNum == t0);
        turn.MoveNext();
        Check("I-3 첫 MoveNext 후 TurnNum 증가", flow3.TurnNum == t0 + 1);

        // ===== I-4: Step6·7 배선 + D5 acted 스킵 (7) =====
        var A = Ally("IA", 5); var B = Ally("IB", 5);
        var flow4 = (BattleFlowSystem)FormatterServices.GetUninitializedObject(typeof(BattleFlowSystem));
        Set(flow4, "_allies", new List<AllyUnit> { A, B });
        Set(flow4, "_enemies", new List<EnemyUnit>());
        Set(flow4, "_intentSystem", new IntentSystem());
        Set(flow4, "_protection", new ProtectionSystem());
        Set(flow4, "_executor", new NoopExecutor());

        var def6 = new List<InputRequest>();
        var s6 = Pump(flow4, "Step6_DefenseResponse");
        while (s6.MoveNext())
            if (s6.Current is InputRequest req)
            {
                def6.Add(req);
                if (req.DecidingUnit == A) req.SetResponse(ActionCommand.CreateDefense(A, AttackDirection.High));
                else req.SetResponse(ActionCommand.CreateEndTurn(req.DecidingUnit));
            }
        Check("I-4 Step6 요청 A→B 순서", def6.Count == 2 && def6[0].DecidingUnit == A && def6[1].DecidingUnit == B);
        Check("I-4 A 방어 stance 적용(턴 내)", A.GetDefenseStance().Defense == AttackDirection.High);
        Check("I-4 A acted", A.ActedThisTurn);
        Check("I-4 B 미대응 acted false", !B.ActedThisTurn);

        var act7 = new List<InputRequest>();
        var s7 = Pump(flow4, "Step7_ExecuteBySpeed");
        while (s7.MoveNext())
            if (s7.Current is InputRequest req7)
            {
                act7.Add(req7);
                req7.SetResponse(ActionCommand.CreateEndTurn(req7.DecidingUnit));
            }
        Check("I-4 D5 대응한 A는 Action 요청 없음", act7.All(q => q.DecidingUnit != A));
        Check("I-4 미대응 B는 Action 요청 받음", act7.Any(q => q.DecidingUnit == B));
        Check("I-4 Step7 Phase 전부 Action", act7.All(q => q.Phase == InputPhase.Action));

        // ===== I-5: RunBattle 완주 + D4 (4) =====
        var wAlly = Ally("W", 10);
        var wEnemy = Enemy("WE");
        var (ctx5, flow5) = Build(new List<AllyUnit> { wAlly }, new List<EnemyUnit> { wEnemy });
        var atk = Skill("atk", 0, dmg: 200, rule: TargetRule.Single);
        var script = new List<ActionCommand>
        {
            ActionCommand.CreateEndTurn(wAlly),          // Step5 Info 포기
            ActionCommand.CreateEndTurn(wAlly),          // Step6 Defense 포기
            ActionCommand.CreateSkill(wAlly, atk, wEnemy) // Step7 공격
        };
        var outcome = ctx5.RunBattle(new ScriptedInputSource(script));
        Check("I-5 스킬 실행 → 적 처치", wEnemy.IsIncapacitated);
        Check("I-5 RunBattle 완주 → Victory", outcome == BattleOutcome.Victory);
        Check("I-5 반환값 == Flow.LastOutcome", outcome == flow5.LastOutcome);
        CheckThrow("I-5 null 소스 방어", () => ctx5.RunBattle(null));

        Console.WriteLine();
        Console.WriteLine($"=== I 프로브 결과: PASS {_pass} / FAIL {_fail} ===");
        return _fail == 0 ? 0 : 1;
    }
}
