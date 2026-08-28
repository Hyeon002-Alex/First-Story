// K 대주제 통합 프로브 (복구본).
// K-1 IntentSystem.GetView(reveal 게이트) / K-2 ChoiceQuerySystem.GetChoices(선택지 산출) /
// K-3 예상=실제 불변식(ActionChoice.PreviewDamages ↔ ActionResolver.PreviewDamage 동일 경로).
// [사후-4] 5단계 인계문서가 "신규, 31 PASS, 커밋 대상"이라 명세했으나 실제로는 dev에 없었음(K 미커밋 결함).
// 이 파일은 그 결함을 회수해 K/L 커밋 코드(TargetSide·ChallengeSystem 포함) 기준으로 재작성한 것.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public static class KProbe
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

    static SkillData Skill(
        string id, int ap, TargetRule rule, TargetSide side,
        bool isInfoAction = false, int dmg = 0)
    {
        var s = new SkillData();
        Set(s, "_skillId", id); Set(s, "_displayName", id + "_이름");
        Set(s, "_apCost", ap); Set(s, "_isInfoAction", isInfoAction);
        Set(s, "_direction", AttackDirection.High);
        Set(s, "_targetRule", rule); Set(s, "_targetSide", side);
        Set(s, "_fixedDamage", dmg);
        Set(s, "_isUnavoidable", false);
        Set(s, "_effects", new List<StatusEffectData>());
        return s;
    }

    static AllyUnit Ally(
        string id, int ap, SkillData unique = null, List<SkillData> equipped = null, bool incap = false)
    {
        var d = new AllyUnitData();
        Set(d, "_unitId", id); Set(d, "_displayName", id); Set(d, "_baseStats", Stats(100));
        Set(d, "_uniqueAction", unique);
        Set(d, "_equippedSkills", equipped ?? new List<SkillData>());
        var a = new AllyUnit(d);
        a.SetAP(ap);
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

    static BattleSnapshot Snapshot(List<AllyUnit> allies, List<EnemyUnit> enemies, int turn = 1)
        => new BattleSnapshot(turn, allies, enemies);

    public static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        UnityEngine.Debug.Muted = true;

        // ===== K-1: IntentSystem.GetView (reveal 게이트) (9) =====
        var intent = new IntentSystem();
        var ke1 = Enemy("KE1");
        var kTarget = Ally("KT", 5);
        var kSkill = Skill("k1", 0, TargetRule.Single, TargetSide.Hostile);
        Set(kSkill, "_isUnavoidable", true);
        Set(kSkill, "_effects", new List<StatusEffectData>());

        Check("K-1 미등록 적 GetView null", intent.GetView(ke1) == null);

        intent.SetIntent(ke1, new EnemyIntent(kSkill, kTarget));
        IntentView basic = intent.GetView(ke1);
        Check("K-1 등록 직후 IsRevealed false", !basic.IsRevealed);
        Check("K-1 미확인 Target 노출", ReferenceEquals(basic.Target, kTarget));
        Check("K-1 미확인 Direction 노출", basic.Direction == AttackDirection.High);
        Check("K-1 미확인 DisplayName null", basic.DisplayName == null);
        Check("K-1 미확인 Effects 빈 목록", basic.Effects.Count == 0);
        Check("K-1 미확인 IsUnavoidable false(실제 true여도 은닉)", !basic.IsUnavoidable);

        intent.SetRevealed(ke1);
        IntentView full = intent.GetView(ke1);
        Check("K-1 확인 후 IsRevealed true", full.IsRevealed);
        Check("K-1 확인 후 DisplayName 노출", full.DisplayName == "k1_이름");
        Check("K-1 확인 후 IsUnavoidable 실값 노출", full.IsUnavoidable);

        // ===== K-2: InfoChoices (7) =====
        var infoSkill = Skill("info", 2, TargetRule.Single, TargetSide.Hostile, isInfoAction: true);
        var nonInfoUnique = Skill("nu", 1, TargetRule.Single, TargetSide.Hostile, isInfoAction: false);
        var infoAlly = Ally("IA", 5, unique: infoSkill);
        var infoEnemy1 = Enemy("IE1");
        // BattleSnapshot 계약: LivingEnemies는 생존 필터링 완료본만 담김(생성 측 책임, 소비 측 재필터 안 함)
        // -> 전투불능 적은 애초에 스냅샷에 실리지 않음. 여기선 그 계약이 지켜진 정상 스냅샷으로 검증
        var infoSnap = Snapshot(new List<AllyUnit> { infoAlly }, new List<EnemyUnit> { infoEnemy1 });

        AllyChoices infoChoices = ChoiceQuerySystem.GetChoices(infoAlly, InputPhase.Info, infoSnap);
        Check("K-2 Info 정보형 자격 -> InfoAction+EndTurn 2개",
            infoChoices.Choices.Count == 2 && infoChoices.Choices.Any(c => c.Kind == ActionKind.UniqueAction));
        var infoOffer = infoChoices.Choices.First(c => c.Kind == ActionKind.UniqueAction);
        Check("K-2 Info 대상=스냅샷의 생존 적",
            infoOffer.ValidTargets.Count == 1 && ReferenceEquals(infoOffer.ValidTargets[0], infoEnemy1));
        Check("K-2 Info ApCost 스킬값 그대로", infoOffer.ApCost == 2);

        var poorInfoAlly = Ally("PIA", 1, unique: infoSkill);
        var poorChoices = ChoiceQuerySystem.GetChoices(poorInfoAlly, InputPhase.Info,
            Snapshot(new List<AllyUnit> { poorInfoAlly }, new List<EnemyUnit> { infoEnemy1 }));
        Check("K-2 Info AP부족 -> EndTurn만", poorChoices.Choices.Count == 1 && poorChoices.Choices[0].Kind == ActionKind.EndTurn);

        var nonInfoAlly = Ally("NIA", 5, unique: nonInfoUnique);
        var nonInfoChoices = ChoiceQuerySystem.GetChoices(nonInfoAlly, InputPhase.Info,
            Snapshot(new List<AllyUnit> { nonInfoAlly }, new List<EnemyUnit> { infoEnemy1 }));
        Check("K-2 Info 비정보형 고유행동은 자격 없음 -> EndTurn만",
            nonInfoChoices.Choices.Count == 1 && nonInfoChoices.Choices[0].Kind == ActionKind.EndTurn);

        var noUniqueAlly = Ally("NUA", 5);
        var noUniqueChoices = ChoiceQuerySystem.GetChoices(noUniqueAlly, InputPhase.Info,
            Snapshot(new List<AllyUnit> { noUniqueAlly }, new List<EnemyUnit> { infoEnemy1 }));
        Check("K-2 Info 고유행동 없음(null) -> EndTurn만",
            noUniqueChoices.Choices.Count == 1 && noUniqueChoices.Choices[0].Kind == ActionKind.EndTurn);

        // ===== K-2: DefenseChoices (6) =====
        var defAlly = Ally("DA", 5);
        var defAlly2 = Ally("DA2", 5);
        var defSnap = Snapshot(new List<AllyUnit> { defAlly, defAlly2 }, new List<EnemyUnit>());
        var defChoices = ChoiceQuerySystem.GetChoices(defAlly, InputPhase.Defense, defSnap);
        Check("K-2 Defense AP충분 -> 방향3+보호1+EndTurn = 5",
            defChoices.Choices.Count == 5);
        Check("K-2 Defense 방향방어 3방향 전부", new[] { AttackDirection.High, AttackDirection.Mid, AttackDirection.Low }
            .All(dir => defChoices.Choices.Any(c => c.Kind == ActionKind.Defense && c.Direction == dir)));
        var protOffer = defChoices.Choices.First(c => c.Kind == ActionKind.Protection);
        Check("K-2 Defense 보호 대상=자기 제외 생존 아군",
            protOffer.ValidTargets.Count == 1 && ReferenceEquals(protOffer.ValidTargets[0], defAlly2));

        var poorDefAlly = Ally("PDA", ResponsePhaseSystem.DefenseAPCost);
        var poorDefChoices = ChoiceQuerySystem.GetChoices(poorDefAlly, InputPhase.Defense,
            Snapshot(new List<AllyUnit> { poorDefAlly }, new List<EnemyUnit>()));
        Check("K-2 Defense 보호비용 미달 시 보호 오퍼 생략",
            !poorDefChoices.Choices.Any(c => c.Kind == ActionKind.Protection));

        var soloDefAlly = Ally("SDA", 5);
        var soloDefChoices = ChoiceQuerySystem.GetChoices(soloDefAlly, InputPhase.Defense,
            Snapshot(new List<AllyUnit> { soloDefAlly }, new List<EnemyUnit>()));
        Check("K-2 Defense 혼자 생존 -> 보호 대상 0명, 오퍼 생략",
            !soloDefChoices.Choices.Any(c => c.Kind == ActionKind.Protection));

        var brokeAlly = Ally("BA", 0);
        var brokeChoices = ChoiceQuerySystem.GetChoices(brokeAlly, InputPhase.Defense,
            Snapshot(new List<AllyUnit> { brokeAlly, defAlly2 }, new List<EnemyUnit>()));
        Check("K-2 Defense AP=0 -> EndTurn만", brokeChoices.Choices.Count == 1 && brokeChoices.Choices[0].Kind == ActionKind.EndTurn);

        // ===== K-2: ActionChoices — TargetSide/예상피해 (8) =====
        var atkSkill = Skill("atk", 1, TargetRule.Single, TargetSide.Hostile, dmg: 30);
        var healSkill = Skill("heal", 1, TargetRule.Single, TargetSide.Friendly, dmg: 0);
        var actAlly = Ally("ACA", 5, unique: null, equipped: new List<SkillData> { atkSkill, healSkill });
        var actEnemy = Enemy("ACE");
        var actAllyMate = Ally("ACM", 5);
        var actSnap = Snapshot(new List<AllyUnit> { actAlly, actAllyMate }, new List<EnemyUnit> { actEnemy });

        var actChoices = ChoiceQuerySystem.GetChoices(actAlly, InputPhase.Action, actSnap);
        var atkOffer = actChoices.Choices.FirstOrDefault(c => c.Skill == atkSkill);
        var healOffer = actChoices.Choices.FirstOrDefault(c => c.Skill == healSkill);
        Check("K-2 Action Hostile 스킬 대상=적", atkOffer != null &&
            atkOffer.ValidTargets.Count == 1 && ReferenceEquals(atkOffer.ValidTargets[0], actEnemy));
        Check("K-2 Action Friendly 스킬 대상=아군(자기 포함 가능)", healOffer != null &&
            healOffer.ValidTargets.Count == 2);
        Check("K-2 Action 피해 스킬 PreviewDamages 채워짐", atkOffer.PreviewDamages.Count == 1);
        DamageResult expectPreview = ActionResolver.PreviewDamage(actAlly, actEnemy, atkSkill);
        Check("K-2 예상=실제: PreviewDamages 값이 ActionResolver.PreviewDamage와 동일",
            atkOffer.PreviewDamages[actEnemy].FinalDamage == expectPreview.FinalDamage);
        Check("K-2 Action 무피해 스킬 PreviewDamages 빈 dict", healOffer.PreviewDamages.Count == 0);
        Check("K-2 Action EquippedSkills 2개 + EndTurn = 3개 오퍼",
            actChoices.Choices.Count == 3);

        var infoUniqueActAlly = Ally("IUA", 5, unique: infoSkill,
            equipped: new List<SkillData> { atkSkill });
        var infoUniqueActChoices = ChoiceQuerySystem.GetChoices(infoUniqueActAlly, InputPhase.Action,
            Snapshot(new List<AllyUnit> { infoUniqueActAlly }, new List<EnemyUnit> { actEnemy }));
        Check("K-2 Action 정보형 고유행동은 Info 소관이라 제외",
            !infoUniqueActChoices.Choices.Any(c => c.Skill == infoSkill));

        var apPoorActAlly = Ally("APA", 0, unique: null, equipped: new List<SkillData> { atkSkill });
        var apPoorActChoices = ChoiceQuerySystem.GetChoices(apPoorActAlly, InputPhase.Action,
            Snapshot(new List<AllyUnit> { apPoorActAlly }, new List<EnemyUnit> { actEnemy }));
        Check("K-2 Action AP부족 스킬 생략 -> EndTurn만",
            apPoorActChoices.Choices.Count == 1 && apPoorActChoices.Choices[0].Kind == ActionKind.EndTurn);

        // ===== K-2: GetValidTargets 규칙 해소 (4) =====
        var self = Ally("SELF", 5);
        var pool = new List<BattleUnit> { actEnemy, actAllyMate };
        Check("K-2 GetValidTargets Self -> [actor]",
            ChoiceQuerySystem.GetValidTargets(self, TargetRule.Single, pool).Count == 2
            && !ChoiceQuerySystem.GetValidTargets(self, TargetRule.Self, pool).Contains(actEnemy)
            && ChoiceQuerySystem.GetValidTargets(self, TargetRule.Self, pool).Single() == self);
        Check("K-2 GetValidTargets Single -> pool 그대로",
            ChoiceQuerySystem.GetValidTargets(self, TargetRule.Single, pool).SequenceEqual(pool));
        Check("K-2 GetValidTargets Area -> pool 그대로",
            ChoiceQuerySystem.GetValidTargets(self, TargetRule.Area, pool).SequenceEqual(pool));
        Check("K-2 GetValidTargets FixedTarget -> pool 그대로",
            ChoiceQuerySystem.GetValidTargets(self, TargetRule.FixedTarget, pool).SequenceEqual(pool));

        // ===== ActionChoice 팩토리 가드 (2) =====
        CheckThrow("ActionChoice.Defense(None) throw", () => ActionChoice.Defense(AttackDirection.None, 1));
        CheckThrow("ActionChoice 대상 필수 오퍼 후보0 throw",
            () => ActionChoice.Protection(1, new List<BattleUnit>()));

        Console.WriteLine();
        Console.WriteLine($"=== K 프로브 결과: PASS {_pass} / FAIL {_fail} ===");
        return _fail == 0 ? 0 : 1;
    }
}