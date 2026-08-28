// J 대주제 통합 프로브 (완전 재구성). 정보형 고유행동 = UniqueAction + IsInfoAction.
// J-1 Reveal 모델 통일 / J-2 InfoResponseSystem / J-3 Step5·Step6 배선.
// 조립: 유닛·SO는 정상 생성자 + 통로. BattleFlowSystem은 GetUninitializedObject + 3필드 주입.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

public static class JProbe
{
    static int _pass, _fail;
    static void Check(string name, bool cond)
    {
        if (cond) { _pass++; }
        else { _fail++; Console.WriteLine("  [FAIL] " + name); }
    }
    static void CheckThrow(string name, Action act)
    {
        try { act(); Check(name, false); }
        catch { Check(name, true); }
    }

    static void Set(object o, string field, object val)
    {
        FieldInfo f = null;
        for (Type t = o.GetType(); t != null && f == null; t = t.BaseType)
            f = t.GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (f == null) throw new Exception("필드 없음: " + field + " on " + o.GetType());
        f.SetValue(o, val);
    }

    static UnitStats Stats(int hp)
    {
        object box = default(UnitStats);
        Set(box, "_maxHP", hp); Set(box, "_attack", 10);
        Set(box, "_defense", 10); Set(box, "_speed", 10);
        return (UnitStats)box;
    }
    static SkillData Skill(string id, int ap, bool info)
    {
        var s = new SkillData();
        Set(s, "_skillId", id); Set(s, "_displayName", id);
        Set(s, "_apCost", ap); Set(s, "_isInfoAction", info);
        Set(s, "_direction", AttackDirection.None);
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
    static EnemyUnit Enemy(string id, bool incap = false)
    {
        var d = new EnemyUnitData();
        Set(d, "_enemyId", id); Set(d, "_displayName", id); Set(d, "_baseStats", Stats(100));
        var e = new EnemyUnit(d);
        if (incap) e.SetIncapacitated(true);
        return e;
    }

    public static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        UnityEngine.Debug.Muted = true;

        // === J-1: Reveal 모델 통일 (3) ===
        Check("J-1 ActionKind.Reveal 부재", Array.IndexOf(Enum.GetNames(typeof(ActionKind)), "Reveal") < 0);
        Check("J-1 ActionCommand.CreateReveal 부재",
            typeof(ActionCommand).GetMethod("CreateReveal", BindingFlags.Public | BindingFlags.Static) == null);
        Check("J-1 SkillData.IsInfoAction 존재", typeof(SkillData).GetProperty("IsInfoAction") != null);

       // === J-2: TryValidate 정상 — 순수 검증, 상태 미변경 (5) ===
// 사후-2: InfoResponseSystem은 이제 IntentSystem을 참조하지 않음(순수 검증으로 축소).
// reveal/AP/acted 상태 변경 검증은 그 오케스트레이션을 소유하는 Step5_InfoResponse(J-3 섹션)에서 확인.
int apBeforeValidate = ally.CurrAP;
bool r = InfoResponseSystem.TryValidate(infoCmd, out AllyUnit vAlly, out EnemyUnit vEnemy, out int vApCost);
Check("J-2 TryValidate 반환 true", r);
Check("J-2 TryValidate out ally", ReferenceEquals(vAlly, ally));
Check("J-2 TryValidate out enemy", ReferenceEquals(vEnemy, enemy));
Check("J-2 TryValidate out apCost", vApCost == 2);
Check("J-2 TryValidate는 상태 미변경(AP 그대로)", ally.CurrAP == apBeforeValidate);

// === J-2: TryValidate AP부족 거부 (4) ===
var poor = Ally("P", 1);
var enemy2 = Enemy("E2");
bool r2 = InfoResponseSystem.TryValidate(
    ActionCommand.CreateUnique(poor, Skill("i2", 2, true), enemy2), out AllyUnit a2, out EnemyUnit e2, out int c2);
Check("J-2 AP부족 반환 false", !r2);
Check("J-2 AP부족 out ally 그대로", ReferenceEquals(a2, poor));
Check("J-2 AP부족 out enemy 미확정(null)", e2 == null);
Check("J-2 AP부족 AP 무변", poor.CurrAP == 1);

// === J-2: 대상 무효 (2) ===
Check("J-2 전투불능 적 대상 false",
    !InfoResponseSystem.TryValidate(
        ActionCommand.CreateUnique(Ally("A2", 5), Skill("i3", 2, true), Enemy("E3", true)), out _, out _, out _));
Check("J-2 아군 대상 false",
    !InfoResponseSystem.TryValidate(
        ActionCommand.CreateUnique(Ally("A3", 5), Skill("i4", 2, true), Ally("A4", 5)), out _, out _, out _));

// === J-2: throw (3) ===
CheckThrow("J-2 비정보 명령 throw",
    () => InfoResponseSystem.TryValidate(
        ActionCommand.CreateDefense(Ally("A5", 5), AttackDirection.High), out _, out _, out _));
CheckThrow("J-2 null command throw",
    () => InfoResponseSystem.TryValidate(null, out _, out _, out _));
CheckThrow("J-2 비아군 주체 throw",
    () => InfoResponseSystem.TryValidate(
        ActionCommand.CreateUnique(Enemy("E4"), Skill("i5", 2, true), Enemy("E5")), out _, out _, out _));

        // === J-3: Step5·Step6 배선 펌프 (10) ===
        var allyA = Ally("A", 5);
        var allyB = Ally("B", 5);
        var deadAlly = Ally("D", 5, incap: true);
        var alliesList = new List<AllyUnit> { allyA, allyB, deadAlly };
        var targetEnemy = Enemy("TE");
        var flowIntent = new IntentSystem();
        var infoSkill2 = Skill("info2", 2, true);
        int allyAApBefore = allyA.CurrAP;

        var flow = (BattleFlowSystem)FormatterServices.GetUninitializedObject(typeof(BattleFlowSystem));
        var flowProtection = new ProtectionSystem();
        Set(flow, "_allies", alliesList);
        Set(flow, "_intentSystem", flowIntent);
        Set(flow, "_protection", flowProtection);
        // L-3: Step5_InfoResponse가 정보형 공격겸용 실행에 executor를 씀. ActionResolver는 같은 protection 인스턴스 공유(실제 조립과 동일)
        Set(flow, "_executor",
            new ActionResolver(alliesList, new List<EnemyUnit> { targetEnemy }, flowProtection, flowIntent));

        // Step5 펌프
        var step5m = typeof(BattleFlowSystem).GetMethod("Step5_InfoResponse", BindingFlags.NonPublic | BindingFlags.Instance);
        IEnumerator it5 = (IEnumerator)step5m.Invoke(flow, null);
        var req5 = new List<InputRequest>();
        while (it5.MoveNext())
            if (it5.Current is InputRequest rq)
            {
                req5.Add(rq);
                if (rq.DecidingUnit == allyA)
                    rq.SetResponse(ActionCommand.CreateUnique(allyA, infoSkill2, targetEnemy));
                else
                    rq.SetResponse(ActionCommand.CreateEndTurn(rq.DecidingUnit));
            }
        Check("J-3 Step5 생존 아군 2명 요청(Dead 제외)", req5.Count == 2);
        Check("J-3 Step5 모든 요청 Phase=Info", req5.TrueForAll(x => x.Phase == InputPhase.Info));
        Check("J-3 Step5 요청 순서 A->B", req5.Count == 2 && req5[0].DecidingUnit == allyA && req5[1].DecidingUnit == allyB);
        Check("J-3 Step5 Dead 아군 미포함", req5.TrueForAll(x => x.DecidingUnit != deadAlly));
        Check("J-3 A 정보대응 -> 대상 IsRevealed", flowIntent.IsRevealed(targetEnemy));
        Check("J-3 A 정보대응 -> AP 2 감소(Step5 오케스트레이션)", allyA.CurrAP == allyAApBefore - 2);
        Check("J-3 A 정보대응 -> acted", allyA.ActedThisTurn);
        Check("J-3 B 포기(EndTurn) -> acted false", !allyB.ActedThisTurn);

        // Step6 펌프 (같은 flow, acted 유지)
        var step6m = typeof(BattleFlowSystem).GetMethod("Step6_DefenseResponse", BindingFlags.NonPublic | BindingFlags.Instance);
        IEnumerator it6 = (IEnumerator)step6m.Invoke(flow, null);
        var req6 = new List<InputRequest>();
        while (it6.MoveNext())
            if (it6.Current is InputRequest rq6)
            {
                req6.Add(rq6);
                rq6.SetResponse(ActionCommand.CreateEndTurn(rq6.DecidingUnit));
            }
        Check("J-3 Step6 acted된 A 요청 없음", req6.TrueForAll(x => x.DecidingUnit != allyA));
        Check("J-3 Step6 미행동 B 요청 있음", req6.Exists(x => x.DecidingUnit == allyB));
        Check("J-3 Step6 요청 Phase=Defense", req6.TrueForAll(x => x.Phase == InputPhase.Defense));

        Console.WriteLine();
        Console.WriteLine($"=== J 프로브 결과: PASS {_pass} / FAIL {_fail} ===");
        return _fail == 0 ? 0 : 1;
    }
}
