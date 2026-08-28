// L 대주제 통합 프로브. Action 위상 완성.
// L-1 SkillData.TargetSide / L-2 ChoiceQuerySystem.ActionChoices / L-3 정보형 공격겸용 / L-4 PreviewDamages.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

public static class LProbe
{
    static int _pass, _fail;
    static void Check(string name, bool cond)
    {
        if (cond) _pass++;
        else { _fail++; Console.WriteLine("  [FAIL] " + name); }
    }

    const BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
    static void Set(object o, string field, object val)
    {
        FieldInfo f = null;
        for (Type t = o.GetType(); t != null && f == null; t = t.BaseType) f = t.GetField(field, BF);
        if (f == null) throw new Exception("필드 없음: " + field + " on " + o.GetType());
        f.SetValue(o, val);
    }
    static IEnumerator Pump(object flow, string method)
        => (IEnumerator)typeof(BattleFlowSystem).GetMethod(method, BF).Invoke(flow, null);

    static UnitStats Stats(int hp, int atk = 20, int def = 5)
    {
        object box = default(UnitStats);
        Set(box, "_maxHP", hp); Set(box, "_attack", atk); Set(box, "_defense", def); Set(box, "_speed", 10);
        return (UnitStats)box;
    }

    // rule/side 기본값: Hostile/Single(가장 흔한 공격 스킬 모양)
    static SkillData Skill(string id, int ap, TargetRule rule = TargetRule.Single, TargetSide side = TargetSide.Hostile,
        float dmgCoeffi = 0f, int fixedDmg = 0, float healCoeffi = 0f, int breakAmt = 0, bool info = false)
    {
        var s = new SkillData();
        Set(s, "_skillId", id); Set(s, "_displayName", id);
        Set(s, "_apCost", ap); Set(s, "_targetRule", rule); Set(s, "_targetSide", side);
        Set(s, "_damageCoeffi", dmgCoeffi); Set(s, "_fixedDamage", fixedDmg);
        Set(s, "_healingCoeffi", healCoeffi); Set(s, "_breakAmount", breakAmt);
        Set(s, "_isInfoAction", info); Set(s, "_direction", AttackDirection.Mid);
        return s;
    }
    static AllyUnit Ally(string id, int ap, SkillData unique = null, List<SkillData> equipped = null)
    {
        var d = new AllyUnitData();
        Set(d, "_unitId", id); Set(d, "_displayName", id); Set(d, "_baseStats", Stats(100));
        Set(d, "_uniqueAction", unique); Set(d, "_equippedSkills", equipped ?? new List<SkillData>());
        var a = new AllyUnit(d); a.SetAP(ap);
        return a;
    }
    static EnemyUnit Enemy(string id, int hp = 100, int def = 5)
    {
        var d = new EnemyUnitData();
        Set(d, "_enemyId", id); Set(d, "_displayName", id); Set(d, "_baseStats", Stats(hp, def: def));
        Set(d, "_maxBreakGauge", 999);
        return new EnemyUnit(d);
    }

    static ActionChoice Find(AllyChoices choices, string skillId)
        => choices.Choices.FirstOrDefault(c => c.Skill != null && c.Skill.SkillId == skillId);

    public static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        UnityEngine.Debug.Muted = true;

        // ==================== L-1: TargetSide 필드 (2) ====================
        var probeSkill = Skill("probe", 1, side: TargetSide.Friendly);
        Check("L-1 TargetSide 저장값 반환(Friendly)", probeSkill.TargetSide == TargetSide.Friendly);
        var probeSkill2 = Skill("probe2", 1, side: TargetSide.Hostile);
        Check("L-1 TargetSide 저장값 반환(Hostile)", probeSkill2.TargetSide == TargetSide.Hostile);

        // ==================== L-2: ActionChoices 산출 ====================
        var atk = Skill("atk", 1, TargetRule.Single, TargetSide.Hostile);
        var heal = Skill("heal", 2, TargetRule.Single, TargetSide.Friendly, healCoeffi: 1.0f);
        var selfBuff = Skill("buff", 2, TargetRule.Self, TargetSide.Friendly);
        var tooExpensive = Skill("big", 9, TargetRule.Single, TargetSide.Hostile);
        var infoUnique = Skill("info", 1, TargetRule.Single, TargetSide.Hostile, info: true);

        var mainAlly = Ally("A1", 6, atk, new List<SkillData> { heal, selfBuff, tooExpensive });
        var sideAlly = Ally("A2", 6);
        var enemyA = Enemy("E1");
        var enemyB = Enemy("E2");
        var snapA = new BattleSnapshot(1, new List<AllyUnit> { mainAlly, sideAlly }, new List<EnemyUnit> { enemyA, enemyB });

        AllyChoices actionChoices = ChoiceQuerySystem.GetChoices(mainAlly, InputPhase.Action, snapA);

        var atkOffer = Find(actionChoices, "atk");
        Check("L-2 Hostile 고유행동 오퍼 생성", atkOffer != null);
        Check("L-2 Hostile 대상 = 생존 적 전원(2)", atkOffer != null && atkOffer.ValidTargets.Count == 2);
        Check("L-2 고유행동 Kind = UniqueAction", atkOffer != null && atkOffer.Kind == ActionKind.UniqueAction);

        var healOffer = Find(actionChoices, "heal");
        Check("L-2 Friendly 편성스킬 오퍼 생성", healOffer != null);
        Check("L-2 Friendly 대상에 자기 포함", healOffer != null && healOffer.ValidTargets.Contains(mainAlly));
        Check("L-2 Friendly 대상에 다른 아군 포함", healOffer != null && healOffer.ValidTargets.Contains(sideAlly));
        Check("L-2 편성스킬 Kind = Skill", healOffer != null && healOffer.Kind == ActionKind.Skill);

        var buffOffer = Find(actionChoices, "buff");
        Check("L-2 Self 스킬 오퍼 생성(TargetSide 무시)", buffOffer != null);
        Check("L-2 Self 대상 = 자기 1명뿐", buffOffer != null && buffOffer.ValidTargets.Count == 1
            && ReferenceEquals(buffOffer.ValidTargets[0], mainAlly));

        Check("L-2 AP 부족 스킬은 오퍼 생략", Find(actionChoices, "big") == null);

        var infoAlly = Ally("A3", 6, infoUnique);
        var snapInfo = new BattleSnapshot(1, new List<AllyUnit> { infoAlly }, new List<EnemyUnit> { enemyA });
        var infoAllyChoices = ChoiceQuerySystem.GetChoices(infoAlly, InputPhase.Action, snapInfo);
        Check("L-2 정보형 고유행동은 Action 위상에서 제외", Find(infoAllyChoices, "info") == null);

        Check("L-2 차례종료 항상 포함", actionChoices.Choices.Any(c => c.Kind == ActionKind.EndTurn));

        // ==================== L-3: 정보형 공격겸용 실행 ====================
        var tamse = Skill("tamse", 1, TargetRule.Single, TargetSide.Hostile,
            dmgCoeffi: 0.60f, breakAmt: 4, info: true);
        var yeon = Ally("Yeon", 6, tamse);
        var infoEnemy = Enemy("IE", hp: 100);
        var l3Allies = new List<AllyUnit> { yeon };
        var l3Enemies = new List<EnemyUnit> { infoEnemy };
        var l3Intent = new IntentSystem();
        var l3Protection = new ProtectionSystem();
        var l3Executor = new ActionResolver(l3Allies, l3Enemies, l3Protection, l3Intent);

        var l3Flow = (BattleFlowSystem)FormatterServices.GetUninitializedObject(typeof(BattleFlowSystem));
        Set(l3Flow, "_allies", l3Allies);
        Set(l3Flow, "_intentSystem", l3Intent);
        Set(l3Flow, "_protection", l3Protection);
        Set(l3Flow, "_executor", l3Executor);

        int hpBeforeInfo = infoEnemy.CurrHP;
        IEnumerator it5 = Pump(l3Flow, "Step5_InfoResponse");
        while (it5.MoveNext())
            if (it5.Current is InputRequest rq)
                rq.SetResponse(ActionCommand.CreateUnique(yeon, tamse, infoEnemy));

        Check("L-3 정보확인 게이트 ON", l3Intent.IsRevealed(infoEnemy));
        Check("L-3 AP 소모(1)", yeon.CurrAP == 5);
        Check("L-3 행동 소진", yeon.ActedThisTurn);
        Check("L-3 공격 겸용 실행 -> HP 감소", infoEnemy.CurrHP < hpBeforeInfo);
        Check("L-3 붕괴 게이지 누적(4)", infoEnemy.CurrBreakOrCrackGauge == 4);

        // ==================== L-4: PreviewDamages ====================
        var bolt = Skill("bolt", 1, TargetRule.Single, TargetSide.Hostile, dmgCoeffi: 0.80f, breakAmt: 4);
        var healSkill = Skill("healpv", 2, TargetRule.Single, TargetSide.Friendly, healCoeffi: 1.0f);
        var caster = Ally("Ain", 6, bolt, new List<SkillData> { healSkill });
        var e1 = Enemy("PE1", def: 5);
        var e2 = Enemy("PE2", def: 20);   // 방어력 다르게 -> 대상별 프리뷰 값이 실제로 개별 계산되는지 확인
        var snapL4 = new BattleSnapshot(1, new List<AllyUnit> { caster }, new List<EnemyUnit> { e1, e2 });
        var l4Choices = ChoiceQuerySystem.GetChoices(caster, InputPhase.Action, snapL4);

        var boltOffer = Find(l4Choices, "bolt");
        Check("L-4 공격 오퍼 PreviewDamages 대상 수만큼(2)", boltOffer != null && boltOffer.PreviewDamages.Count == 2);
        Check("L-4 예상피해 > 0", boltOffer != null && boltOffer.PreviewDamages[e1].FinalDamage > 0);
        Check("L-4 방어력 다른 대상은 예상피해도 다름",
            boltOffer != null && boltOffer.PreviewDamages[e1].FinalDamage != boltOffer.PreviewDamages[e2].FinalDamage);

        var healOfferPv = Find(l4Choices, "healpv");
        Check("L-4 회복 오퍼 PreviewDamages 빈 목록", healOfferPv != null && healOfferPv.PreviewDamages.Count == 0);

        // 예상=실제 불변식(GDD 11.1)
        int previewed = boltOffer.PreviewDamages[e1].FinalDamage;
        int hpBeforeExec = e1.CurrHP;
        var l4Executor = new ActionResolver(
            new List<AllyUnit> { caster }, new List<EnemyUnit> { e1, e2 }, new ProtectionSystem(), new IntentSystem());
        l4Executor.Execute(ActionCommand.CreateUnique(caster, bolt, e1), 1);
        int actual = hpBeforeExec - e1.CurrHP;
        Check($"L-4 예상=실제 (예상 {previewed} / 실제 {actual})", previewed == actual);

        Console.WriteLine();
        Console.WriteLine($"=== L 프로브 결과: PASS {_pass} / FAIL {_fail} ===");
        return _fail == 0 ? 0 : 1;
    }
}