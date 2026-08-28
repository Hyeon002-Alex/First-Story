// [M 선행조건] 정보확인(Reveal) 지속 정책 프로브.
// 확정 정책(C안, 웨이브 단위 지속): 정보확인은 그 웨이브가 끝날 때까지 유지.
// 매 턴 리셋(A안) 아님 — IntentSystem.ClearTurn()은 intent/cancelled만 비우고 revealed는 유지.
// 웨이브 전환에서만 IntentSystem.ClearRevealed() 호출(BattleFlowSystem.Step9_Judge).
// M(방어위상 정보전 예상피해) 착수 전 이 정책이 코드로 확정돼 있어야 함 — 그 선행조건 검증.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

public static class RevealPersistenceProbe
{
    static int _pass, _fail;
    static void Check(string name, bool cond)
    {
        if (cond) { _pass++; }
        else { _fail++; Console.WriteLine("  [FAIL] " + name); }
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

    static EnemyUnit Enemy(string id)
    {
        var d = new EnemyUnitData();
        Set(d, "_enemyId", id); Set(d, "_displayName", id); Set(d, "_baseStats", Stats(100));
        return new EnemyUnit(d);
    }

    static void InvokeStep3(BattleFlowSystem flow)
        => typeof(BattleFlowSystem).GetMethod("Step3_AssignEnemyIntent", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(flow, null);

    public static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        UnityEngine.Debug.Muted = true;

        // ===== 단위: IntentSystem.ClearTurn/ClearRevealed 분리 (5) =====
        var e1 = Enemy("RE1");
        var t1 = Enemy("RE_TARGET"); // EnemyIntent 생성용 더미 대상(타입 무관, BattleUnit이면 됨. 여기선 아무 유닛)
        var skill = new SkillData();
        Set(skill, "_skillId", "s"); Set(skill, "_displayName", "s");
        Set(skill, "_direction", AttackDirection.High);

        var intent = new IntentSystem();
        intent.SetIntent(e1, new EnemyIntent(skill, t1));
        intent.SetRevealed(e1);
        Check("단위: SetRevealed 직후 IsRevealed true", intent.IsRevealed(e1));

        intent.ClearTurn();
        Check("단위: ClearTurn 후에도 IsRevealed 유지(정책 핵심)", intent.IsRevealed(e1));
        Check("단위: ClearTurn 후 intent는 비워짐(GetIntent null)", intent.GetIntent(e1) == null);

        intent.ClearRevealed();
        Check("단위: ClearRevealed 후 IsRevealed false", !intent.IsRevealed(e1));

        // ClearAll(레거시, 프로브/외부 전체리셋용)은 여전히 둘 다 비움
        intent.SetIntent(e1, new EnemyIntent(skill, t1));
        intent.SetRevealed(e1);
        intent.ClearAll();
        Check("단위: ClearAll은 여전히 revealed까지 전부 비움(레거시 유지)", !intent.IsRevealed(e1));

        // ===== 통합: BattleFlowSystem.Step3가 ClearAll이 아닌 ClearTurn을 쓰는지 (2) =====
        var flowIntent = new IntentSystem();
        var e2 = Enemy("RE2");
        flowIntent.SetIntent(e2, new EnemyIntent(skill, t1));
        flowIntent.SetRevealed(e2);

        var flow = (BattleFlowSystem)FormatterServices.GetUninitializedObject(typeof(BattleFlowSystem));
        Set(flow, "_allies", new List<AllyUnit>());
        Set(flow, "_enemies", new List<EnemyUnit> { e2 });
        Set(flow, "_intentSystem", flowIntent);
        Set(flow, "_challenge", new ChallengeSystem());
        Set(flow, "_behaviorSystem", new EnemyBehaviorSystem(new Dictionary<string, BehaviorPatternData>(), flowIntent));

        InvokeStep3(flow); // 빈 registry -> e2 재결정 안 됨(무행동). 관심사는 revealed 보존 여부

        Check("통합: Step3(턴 경계) 통과 후에도 IsRevealed 유지", flowIntent.IsRevealed(e2));
        Check("통합: Step3가 intent는 갱신(빈 registry라 미등록으로 리셋)", flowIntent.GetIntent(e2) == null);

        Console.WriteLine();
        Console.WriteLine($"=== RevealPersistence 프로브 결과: PASS {_pass} / FAIL {_fail} ===");
        return _fail == 0 ? 0 : 1;
    }
}