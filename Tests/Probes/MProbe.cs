// M 대주제 통합 프로브. 방어위상 정보전 예상피해.
// M-1 ComputeDamage/PreviewDamage 자세 오버라이드 / M-2 IntentSystem.GetAttackers(공격자 역참조) /
// M-3 DefensePreviewSystem.Preview(산출) / M-4 ChoiceQuerySystem.DefenseChoices 배선.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public static class MProbe
{
    static int _pass, _fail;
    static void Check(string name, bool cond)
    {
        if (cond) _pass++;
        else { _fail++; Console.WriteLine("  [FAIL] " + name); }
    }
    static void CheckThrow(string name, Action act)
    {
        try { act(); Check(name, false); }
        catch { Check(name, true); }
    }

    const BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
    static void Set(object o, string field, object val)
    {
        FieldInfo f = null;
        for (Type t = o.GetType(); t != null && f == null; t = t.BaseType) f = t.GetField(field, BF);
        if (f == null) throw new Exception("필드 없음: " + field + " on " + o.GetType());
        f.SetValue(o, val);
    }

    static UnitStats Stats(int hp, int atk = 20, int def = 5)
    {
        object box = default(UnitStats);
        Set(box, "_maxHP", hp); Set(box, "_attack", atk); Set(box, "_defense", def); Set(box, "_speed", 10);
        return (UnitStats)box;
    }

    // rule/side/direction 기본값: Hostile/Single/None(호출부가 필요한 것만 명시)
    static SkillData Skill(string id, int ap, TargetRule rule, TargetSide side,
        AttackDirection direction, float dmgCoeffi = 0f, float healCoeffi = 0f)
    {
        var s = new SkillData();
        Set(s, "_skillId", id); Set(s, "_displayName", id);
        Set(s, "_apCost", ap); Set(s, "_targetRule", rule); Set(s, "_targetSide", side);
        Set(s, "_damageCoeffi", dmgCoeffi); Set(s, "_healingCoeffi", healCoeffi);
        Set(s, "_direction", direction);
        return s;
    }

    static AllyUnit Ally(string id, int ap)
    {
        var d = new AllyUnitData();
        Set(d, "_unitId", id); Set(d, "_displayName", id); Set(d, "_baseStats", Stats(100));
        var a = new AllyUnit(d); a.SetAP(ap);
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

        // ==================== M-1: 자세 오버라이드 ====================
        var m1Attacker = Enemy("M1E");
        var m1Defender = Ally("M1A", 6);
        var m1Skill = Skill("m1atk", 0, TargetRule.Single, TargetSide.Hostile, AttackDirection.Mid, dmgCoeffi: 1.0f);

        // a. 기본(3인자, 자세없음 1.00) vs 가상자세(4인자, 방향일치 0.30)
        DamageResult baseline = ActionResolver.PreviewDamage(m1Attacker, m1Defender, m1Skill);
        Check("M-1a 기본 directionMod = 자세없음(1.00)", baseline.DirectionMod == 1.00f);

        var hypoMatch = new DefenseStance(AttackDirection.Mid, AttackDirection.None, isActive: true);
        DamageResult overridden = ActionResolver.PreviewDamage(m1Attacker, m1Defender, m1Skill, hypoMatch);
        Check("M-1a 가상자세 directionMod = 방향일치(0.30)", overridden.DirectionMod == 0.30f);
        Check("M-1a 가상자세 FinalDamage < 기본", overridden.FinalDamage < baseline.FinalDamage);

        // b. 3인자(실제 자세 위임) == 4인자(같은 자세 명시) 일치 - wrapper 정합성
        m1Defender.SetStance(AttackDirection.High, AttackDirection.None);
        DamageResult viaWrapper = ActionResolver.PreviewDamage(m1Attacker, m1Defender, m1Skill);
        DamageResult viaExplicit = ActionResolver.PreviewDamage(m1Attacker, m1Defender, m1Skill, m1Defender.GetDefenseStance());
        Check("M-1b 3인자==4인자(실제자세 명시) FinalDamage 일치", viaWrapper.FinalDamage == viaExplicit.FinalDamage);
        Check("M-1b 3인자==4인자 DirectionMod 일치", viaWrapper.DirectionMod == viaExplicit.DirectionMod);

        // c. breakMod/receivedDamageMod는 가상자세와 무관 - 대상의 실제 현재 상태만 반영
        m1Defender.SetBreakDamageMod(1.50f, 99);
        var hypoA = new DefenseStance(AttackDirection.Mid, AttackDirection.None, true);
        var hypoB = new DefenseStance(AttackDirection.High, AttackDirection.None, true);
        DamageResult withHypoA = ActionResolver.PreviewDamage(m1Attacker, m1Defender, m1Skill, hypoA);
        DamageResult withHypoB = ActionResolver.PreviewDamage(m1Attacker, m1Defender, m1Skill, hypoB);
        Check("M-1c BreakMod는 가상자세 무관, 대상 실제상태 그대로(1.50)",
            withHypoA.BreakMod == 1.50f && withHypoB.BreakMod == 1.50f);

        // ==================== M-2: 공격자 역참조(IntentSystem.GetAttackers) ====================
        var m2Intent = new IntentSystem();
        var allyA = Ally("AllyA", 6);
        var allyB = Ally("AllyB", 6);
        var singleSkill = Skill("single", 0, TargetRule.Single, TargetSide.Hostile, AttackDirection.Mid, dmgCoeffi: 1.0f);
        var areaSkill = Skill("area", 0, TargetRule.Area, TargetSide.Hostile, AttackDirection.Mid, dmgCoeffi: 1.0f);

        var e1 = Enemy("E1"); m2Intent.SetIntent(e1, new EnemyIntent(singleSkill, allyA)); m2Intent.SetRevealed(e1);
        var e2 = Enemy("E2"); m2Intent.SetIntent(e2, new EnemyIntent(singleSkill, allyA));                          // 미확인(Q1)
        var e3 = Enemy("E3"); m2Intent.SetIntent(e3, new EnemyIntent(singleSkill, allyB)); m2Intent.SetRevealed(e3);
        var e4 = Enemy("E4"); m2Intent.SetIntent(e4, new EnemyIntent(areaSkill, allyA)); m2Intent.SetRevealed(e4);   // Area, 대표=A
        var e5 = Enemy("E5", incap: true); m2Intent.SetIntent(e5, new EnemyIntent(singleSkill, allyA)); m2Intent.SetRevealed(e5);
        var e6 = Enemy("E6"); m2Intent.SetRevealed(e6);                                                              // intent 미등록
        var e7 = Enemy("E7"); m2Intent.SetIntent(e7, new EnemyIntent(singleSkill, null)); m2Intent.SetRevealed(e7);  // 대상상실
        var e8 = Enemy("E8"); m2Intent.SetIntent(e8, new EnemyIntent(areaSkill, e1)); m2Intent.SetRevealed(e8);      // Area인데 대표가 적(자기측)

        var enemiesM2 = new List<EnemyUnit> { e1, e2, e3, e4, e5, e6, e7, e8 };

        IReadOnlyList<EnemyUnit> attackersOfA = m2Intent.GetAttackers(allyA, enemiesM2);
        Check("M-2 A 공격자 = {E1,E4} 2명", attackersOfA.Count == 2 && attackersOfA.Contains(e1) && attackersOfA.Contains(e4));
        Check("M-2 Q1 미확인 제외(E2)", !attackersOfA.Contains(e2));
        Check("M-2 전투불능 제외(E5)", !attackersOfA.Contains(e5));
        Check("M-2 미등록 intent 제외(E6)", !attackersOfA.Contains(e6));
        Check("M-2 대상상실 제외(E7)", !attackersOfA.Contains(e7));
        Check("M-2 Area 비아군 대표 제외(E8)", !attackersOfA.Contains(e8));

        IReadOnlyList<EnemyUnit> attackersOfB = m2Intent.GetAttackers(allyB, enemiesM2);
        Check("M-2 B 공격자 = {E3,E4} 2명", attackersOfB.Count == 2 && attackersOfB.Contains(e3) && attackersOfB.Contains(e4));
        Check("M-2 Q3 Single은 다른 아군에 안 낌(E1이 B에 없음)", !attackersOfB.Contains(e1));
        Check("M-2 Q3 Area는 대표 아니어도 다른 아군에 낌(E4가 B에도)", attackersOfB.Contains(e4));

        // ==================== M-3: DefensePreviewSystem.Preview ====================
        var m3Intent = new IntentSystem();
        var m3Ally = Ally("M3A", 6);
        var atkSkillHigh = Skill("atk3", 0, TargetRule.Single, TargetSide.Hostile, AttackDirection.High, dmgCoeffi: 1.0f);
        var noDmgSkill = Skill("noDmg3", 0, TargetRule.Single, TargetSide.Hostile, AttackDirection.Low, healCoeffi: 1.0f);

        var ex = Enemy("EX"); m3Intent.SetIntent(ex, new EnemyIntent(atkSkillHigh, m3Ally)); m3Intent.SetRevealed(ex);
        var ey = Enemy("EY"); m3Intent.SetIntent(ey, new EnemyIntent(atkSkillHigh, m3Ally)); m3Intent.SetRevealed(ey);
        var ez = Enemy("EZ"); m3Intent.SetIntent(ez, new EnemyIntent(noDmgSkill, m3Ally)); m3Intent.SetRevealed(ez);  // HasDamage 게이팅 대상

        var enemiesM3 = new List<EnemyUnit> { ex, ey, ez };

        var previewHigh = DefensePreviewSystem.Preview(m3Ally, AttackDirection.High, m3Intent, enemiesM3);
        Check("M-3a HasDamage 게이팅: 공격자 2명(EZ 제외)",
            previewHigh.Count == 2 && previewHigh.ContainsKey(ex) && previewHigh.ContainsKey(ey) && !previewHigh.ContainsKey(ez));
        Check("M-3a 방향일치(High) DirectionMod=0.30", previewHigh[ex].DirectionMod == 0.30f);

        var previewMid = DefensePreviewSystem.Preview(m3Ally, AttackDirection.Mid, m3Intent, enemiesM3);
        Check("M-3b 방향불일치(Mid vs 공격High) DirectionMod=0.75(아군 능동 보상)", previewMid[ex].DirectionMod == 0.75f);

        CheckThrow("M-3c Direction.None throw", () => DefensePreviewSystem.Preview(m3Ally, AttackDirection.None, m3Intent, enemiesM3));

        var lonelyAlly = Ally("Lonely", 6);
        var previewEmpty = DefensePreviewSystem.Preview(lonelyAlly, AttackDirection.High, m3Intent, enemiesM3);
        Check("M-3d 아무도 안 노리면 빈 딕셔너리", previewEmpty.Count == 0);

        // e. 예상=실제(GDD 11.1) - 방어위상 프리뷰 버전. 확정 전 프리뷰 == 확정 후 실제 실행 피해
        var predictAlly = Ally("Predict", 6);
        var predictEnemy = Enemy("PredictE");
        var m3bIntent = new IntentSystem();
        m3bIntent.SetIntent(predictEnemy, new EnemyIntent(atkSkillHigh, predictAlly));
        m3bIntent.SetRevealed(predictEnemy);
        var enemiesPredict = new List<EnemyUnit> { predictEnemy };

        int predicted = DefensePreviewSystem.Preview(predictAlly, AttackDirection.High, m3bIntent, enemiesPredict)[predictEnemy].FinalDamage;

        predictAlly.SetStance(AttackDirection.High, AttackDirection.None);   // 방어위상에서 실제로 확정
        var predictResolver = new ActionResolver(
            new List<AllyUnit> { predictAlly }, enemiesPredict, new ProtectionSystem(), new IntentSystem());
        int hpBefore = predictAlly.CurrHP;
        predictResolver.Execute(ActionCommand.CreateUnique(predictEnemy, atkSkillHigh, predictAlly), 1);
        int actualDamage = hpBefore - predictAlly.CurrHP;
        Check($"M-3e 예상=실제 방어위상 (예상 {predicted} / 실제 {actualDamage})", predicted == actualDamage);

        // ==================== M-4: ChoiceQuerySystem.DefenseChoices 배선 ====================
        var m4Intent = new IntentSystem();
        var defAlly = Ally("DefAlly", 6);
        var atkSkillMid = Skill("atk4", 0, TargetRule.Single, TargetSide.Hostile, AttackDirection.Mid, dmgCoeffi: 1.0f);
        var attacker4 = Enemy("Atk4");
        m4Intent.SetIntent(attacker4, new EnemyIntent(atkSkillMid, defAlly));
        m4Intent.SetRevealed(attacker4);
        var snap4 = new BattleSnapshot(1, new List<AllyUnit> { defAlly }, new List<EnemyUnit> { attacker4 });

        AllyChoices withIntent = ChoiceQuerySystem.GetChoices(defAlly, InputPhase.Defense, snap4, m4Intent);
        ActionChoice highOffer = withIntent.Choices.First(c => c.Kind == ActionKind.Defense && c.Direction == AttackDirection.High);
        Check("M-4a intentSystem 제공 시 방향방어 오퍼에 PreviewDamages 부착(1명)",
            highOffer.PreviewDamages.Count == 1 && highOffer.PreviewDamages.ContainsKey(attacker4));

        AllyChoices noIntent = ChoiceQuerySystem.GetChoices(defAlly, InputPhase.Defense, snap4);   // intentSystem 생략(K/L 구 호출부)
        ActionChoice highOfferNoIntent = noIntent.Choices.First(c => c.Kind == ActionKind.Defense && c.Direction == AttackDirection.High);
        Check("M-4b intentSystem 미제공 시 PreviewDamages 빈 목록(K/L 회귀 보존)", highOfferNoIntent.PreviewDamages.Count == 0);

        var poorAlly = Ally("Poor", 0);
        var snapPoor = new BattleSnapshot(1, new List<AllyUnit> { poorAlly }, new List<EnemyUnit> { attacker4 });
        AllyChoices poorChoices = ChoiceQuerySystem.GetChoices(poorAlly, InputPhase.Defense, snapPoor, m4Intent);
        Check("M-4c AP부족 시 방향방어 오퍼 자체 생략(intentSystem 무관)",
            !poorChoices.Choices.Any(c => c.Kind == ActionKind.Defense));

        ActionChoice directOffer = ActionChoice.Defense(AttackDirection.Low, 1);
        Check("M-4d ActionChoice.Defense 2-인자 호출 여전히 동작(NoPreview)", directOffer.PreviewDamages.Count == 0);

        Console.WriteLine();
        Console.WriteLine($"=== M 프로브 결과: PASS {_pass} / FAIL {_fail} ===");
        return _fail == 0 ? 0 : 1;
    }
}