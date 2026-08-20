using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

// G대주제(적 AI 결정) 통합 프로브. 커밋 제외(_Scratch, asmdef 밖)
// 데이터는 전부 [SerializeField] private + 세터 없음 -> 리플렉션으로 주입
// 대주제 완료 4순차 중 1단계: 최종 코드(리뷰 반영 후) 검증. 여기 결과가 리뷰 트리거
public sealed class G_IntegrationProbe : MonoBehaviour
{
    private int _pass;
    private int _fail;

    private void Start()
    {
        RunG1();
        RunG2();
        RunG3();
        RunG4();
        Debug.Log($"===[G 프로브 종료] PASS {_pass} / FAIL {_fail} ===");
    }

    private void Check(string name, bool cond)
    {
        if (cond) { _pass++; Debug.Log($"[PASS] {name}"); }
        else { _fail++; Debug.LogError($"[FAIL] {name}"); }
    }

    // ============================================================
    // G-1: 조건 Evaluate + 패턴 데이터
    // ============================================================
    private void RunG1()
    {
        // Always = 무조건 true (HP 인자는 무관, 유효값 주입)
        Check("G1-1 Always true", BehaviorCondition.CreateAlways().Evaluate(50, 100, 1, 1));

        // SelfHpBelow(0.3) 경계 = 미만(<). currHp/maxHp==threshold이면 false
        BehaviorCondition hp = BehaviorCondition.CreateSelfHpBelow(0.3f);
        Check("G1-2 SelfHpBelow 29% true", hp.Evaluate(29, 100, 1, 1));
        Check("G1-3 SelfHpBelow 경계 30% false", !hp.Evaluate(30, 100, 1, 1));
        Check("G1-4 SelfHpBelow 31% false", !hp.Evaluate(31, 100, 1, 1));

        // TurnNumberMod(3,0): turn%3==0 (HP 무관, 유효값)
        BehaviorCondition mod = BehaviorCondition.CreateTurnNumberMod(3, 0);
        Check("G1-5 TurnMod(3,0) turn3 true", mod.Evaluate(100, 100, 3, 1));
        Check("G1-6 TurnMod(3,0) turn4 false", !mod.Evaluate(100, 100, 4, 1));
        // 나눗수 0 방어 -> 0 나눗셈 예외 없이 false
        Check("G1-7 TurnMod(0,0) 나눗수0 false", !BehaviorCondition.CreateTurnNumberMod(0, 0).Evaluate(100, 100, 3, 1));

        // TurnNumberAtLeast(5): turn>=5
        BehaviorCondition atLeast = BehaviorCondition.CreateTurnNumberAtLeast(5);
        Check("G1-8 TurnAtLeast(5) turn5 true", atLeast.Evaluate(100, 100, 5, 1));
        Check("G1-9 TurnAtLeast(5) turn4 false", !atLeast.Evaluate(100, 100, 4, 1));

        // SurvivingAllyAtLeast(2): 진영 생존수>=2
        BehaviorCondition ally = BehaviorCondition.CreateSurvivingAllyAtLeast(2);
        Check("G1-10 SurvAlly(2) count2 true", ally.Evaluate(100, 100, 1, 2));
        Check("G1-11 SurvAlly(2) count1 false", !ally.Evaluate(100, 100, 1, 1));

        // 결정론: 같은 입력 반복 -> 같은 결과
        bool r1 = hp.Evaluate(29, 100, 1, 1);
        bool r2 = hp.Evaluate(29, 100, 1, 1);
        Check("G1-12 결정론(반복 동일)", r1 == r2 && r1);

        // 패턴 Rules 순서 보존(첫 매치 전제)
        BehaviorPatternData p = MakePattern("pat_order", new List<BehaviorRule>
        {
            new BehaviorRule(BehaviorCondition.CreateAlways(), "s0", TargetPolicy.FirstAlive),
            new BehaviorRule(BehaviorCondition.CreateAlways(), "s1", TargetPolicy.FirstAlive),
        });
        Check("G1-13 Rules 순서 보존", p.Rules.Count == 2 && p.Rules[0].SkillId == "s0" && p.Rules[1].SkillId == "s1");
    }

    // ============================================================
    // G-2: DataDrivenBehavior 첫매치·폴백·skillId 해석·대상정책·결정론
    // ============================================================
    private void RunG2()
    {
        SkillData skillA = MakeSkill("skA", TargetRule.Single);
        SkillData skillB = MakeSkill("skB", TargetRule.Single);
        SkillData selfSkill = MakeSkill("skSelf", TargetRule.Self);

        // 첫 매치: [SelfHpBelow(0.5)->A, Always->B]
        BehaviorPatternData pat = MakePattern("pat_firstmatch", new List<BehaviorRule>
        {
            new BehaviorRule(BehaviorCondition.CreateSelfHpBelow(0.5f), "skA", TargetPolicy.FirstAlive),
            new BehaviorRule(BehaviorCondition.CreateAlways(), "skB", TargetPolicy.FirstAlive),
        });
        DataDrivenBehavior beh = new DataDrivenBehavior(pat);

        // HP 40% -> 첫 규칙 매치 -> skA
        EnemyUnit low = new EnemyUnit(MakeEnemyData("Elow", MakeStats(100, 10, 0, 5), new List<SkillData> { skillA, skillB }));
        low.ModifyHP(-60);   // 100 -> 40 (40%)
        AllyUnit a0 = new AllyUnit(MakeAllyData("A0", MakeStats(100, 10, 0, 5)));
        BattleSnapshot snap1 = new BattleSnapshot(1, new List<AllyUnit> { a0 }, new List<EnemyUnit> { low });
        EnemyIntent i1 = beh.Decide(snap1, low);
        Check("G2-1 첫매치 HP40% -> skA", i1 != null && i1.Skill.SkillId == "skA");

        // HP 60% -> 첫 규칙 실패 -> 폴백 skB
        EnemyUnit high = new EnemyUnit(MakeEnemyData("Ehigh", MakeStats(100, 10, 0, 5), new List<SkillData> { skillA, skillB }));
        high.ModifyHP(-40);   // 100 -> 60 (60%)
        BattleSnapshot snap2 = new BattleSnapshot(1, new List<AllyUnit> { a0 }, new List<EnemyUnit> { high });
        EnemyIntent i2 = beh.Decide(snap2, high);
        Check("G2-2 폴백 HP60% -> skB", i2 != null && i2.Skill.SkillId == "skB");

        // 폴백 유일: Always만 -> 항상 매치
        BehaviorPatternData onlyAlways = MakePattern("pat_always", new List<BehaviorRule>
        {
            new BehaviorRule(BehaviorCondition.CreateAlways(), "skB", TargetPolicy.FirstAlive),
        });
        EnemyUnit e2 = new EnemyUnit(MakeEnemyData("E2", MakeStats(100, 10, 0, 5), new List<SkillData> { skillB }));
        EnemyIntent i3 = new DataDrivenBehavior(onlyAlways).Decide(
            new BattleSnapshot(1, new List<AllyUnit> { a0 }, new List<EnemyUnit> { e2 }), e2);
        Check("G2-3 폴백 항상 매치", i3 != null && i3.Skill.SkillId == "skB");

        // skillId 미보유 규칙 스킵 -> 다음 규칙: [Always->missing, Always->skB], enemy는 skB만 보유
        BehaviorPatternData missing = MakePattern("pat_missing", new List<BehaviorRule>
        {
            new BehaviorRule(BehaviorCondition.CreateAlways(), "notOwned", TargetPolicy.FirstAlive),
            new BehaviorRule(BehaviorCondition.CreateAlways(), "skB", TargetPolicy.FirstAlive),
        });
        EnemyUnit e3 = new EnemyUnit(MakeEnemyData("E3", MakeStats(100, 10, 0, 5), new List<SkillData> { skillB }));
        EnemyIntent i4 = new DataDrivenBehavior(missing).Decide(
            new BattleSnapshot(1, new List<AllyUnit> { a0 }, new List<EnemyUnit> { e3 }), e3);
        Check("G2-4 미보유 skillId 규칙 스킵 -> 다음", i4 != null && i4.Skill.SkillId == "skB");

        // 대상정책 LowestHp: [A100, A30, A30] -> 슬롯1(첫 30, tie-break 슬롯앞)
        AllyUnit t100 = new AllyUnit(MakeAllyData("T100", MakeStats(100, 10, 0, 5)));
        AllyUnit t30a = new AllyUnit(MakeAllyData("T30a", MakeStats(100, 10, 0, 5))); t30a.ModifyHP(-70);
        AllyUnit t30b = new AllyUnit(MakeAllyData("T30b", MakeStats(100, 10, 0, 5))); t30b.ModifyHP(-70);
        Check("G2-5 LowestHp 동점 tie-break 슬롯앞",
            TargetSelectionPolicy.Select(TargetPolicy.LowestHP, new List<AllyUnit> { t100, t30a, t30b }) == t30a);

        // HighestHp: [A30, A100] -> 슬롯1
        Check("G2-6 HighestHp",
            TargetSelectionPolicy.Select(TargetPolicy.HighestHP, new List<AllyUnit> { t30a, t100 }) == t100);

        // HighestAttack: [atk10, atk50, atk50] -> 슬롯1(첫 50)
        AllyUnit atk10 = new AllyUnit(MakeAllyData("atk10", MakeStats(100, 10, 0, 5)));
        AllyUnit atk50a = new AllyUnit(MakeAllyData("atk50a", MakeStats(100, 50, 0, 5)));
        AllyUnit atk50b = new AllyUnit(MakeAllyData("atk50b", MakeStats(100, 50, 0, 5)));
        Check("G2-7 HighestAttack 동점 tie-break 슬롯앞",
            TargetSelectionPolicy.Select(TargetPolicy.HighestAttack, new List<AllyUnit> { atk10, atk50a, atk50b }) == atk50a);

        // FirstAlive: 슬롯0
        Check("G2-8 FirstAlive 슬롯0",
            TargetSelectionPolicy.Select(TargetPolicy.FirstAlive, new List<AllyUnit> { t100, t30a }) == t100);

        // 대상 후보 없음 -> null
        Check("G2-9 후보 없음 -> null",
            TargetSelectionPolicy.Select(TargetPolicy.LowestHP, new List<AllyUnit>()) == null);

        // Self 스킬 -> target == self
        EnemyUnit selfE = new EnemyUnit(MakeEnemyData("selfE", MakeStats(100, 10, 0, 5), new List<SkillData> { selfSkill }));
        BehaviorPatternData selfPat = MakePattern("pat_self", new List<BehaviorRule>
        {
            new BehaviorRule(BehaviorCondition.CreateAlways(), "skSelf", TargetPolicy.FirstAlive),
        });
        EnemyIntent iSelf = new DataDrivenBehavior(selfPat).Decide(
            new BattleSnapshot(1, new List<AllyUnit> { a0 }, new List<EnemyUnit> { selfE }), selfE);
        Check("G2-10 Self 스킬 -> target==self", iSelf != null && ReferenceEquals(iSelf.Target, selfE));

        // 대상 후보 전멸 -> Single 스킬 target null(불발 계약)
        EnemyUnit e4 = new EnemyUnit(MakeEnemyData("E4", MakeStats(100, 10, 0, 5), new List<SkillData> { skillB }));
        EnemyIntent iNoTarget = new DataDrivenBehavior(onlyAlways).Decide(
            new BattleSnapshot(1, new List<AllyUnit>(), new List<EnemyUnit> { e4 }), e4);
        Check("G2-11 대상 전멸 -> target null", iNoTarget != null && iNoTarget.Target == null);

        // 진영 뒤집힘: SurvivingAllyAtLeast(2)가 LivingEnemies.Count(=적 진영)를 봄. 아군 수 무관
        BehaviorPatternData factionPat = MakePattern("pat_faction", new List<BehaviorRule>
        {
            new BehaviorRule(BehaviorCondition.CreateSurvivingAllyAtLeast(2), "skA", TargetPolicy.FirstAlive),
            new BehaviorRule(BehaviorCondition.CreateAlways(), "skB", TargetPolicy.FirstAlive),
        });
        EnemyUnit f1 = new EnemyUnit(MakeEnemyData("F1", MakeStats(100, 10, 0, 5), new List<SkillData> { skillA, skillB }));
        EnemyUnit f2 = new EnemyUnit(MakeEnemyData("F2", MakeStats(100, 10, 0, 5), new List<SkillData> { skillA, skillB }));
        // 아군 1명, 적 2마리 -> 적 진영 2 >= 2 참 -> skA
        EnemyIntent iFaction = new DataDrivenBehavior(factionPat).Decide(
            new BattleSnapshot(1, new List<AllyUnit> { a0 }, new List<EnemyUnit> { f1, f2 }), f1);
        Check("G2-12 진영뒤집힘 SurvAlly=적진영수", iFaction != null && iFaction.Skill.SkillId == "skA");

        // 결정론: 같은 스냅샷·self 반복 -> 같은 스킬·대상
        EnemyIntent d1 = beh.Decide(snap1, low);
        EnemyIntent d2 = beh.Decide(snap1, low);
        Check("G2-13 결정론(반복 동일)",
            d1.Skill.SkillId == d2.Skill.SkillId && ReferenceEquals(d1.Target, d2.Target));
    }

    // ============================================================
    // G-3: EnemyBehaviorSystem 팩토리·lazy bind·DecideAll + BattleFlow Step3 교체
    // ============================================================
    private void RunG3()
    {
        SkillData skA = MakeSkill("gskA", TargetRule.Single);
        SkillData skB = MakeSkill("gskB", TargetRule.Single);

        // 패턴: Always -> skB(두번째 보유 스킬). 첫 스킬(skA) 아님 = 더미 아님 검증용
        BehaviorPatternData pat = MakePattern("pat_g3", new List<BehaviorRule>
        {
            new BehaviorRule(BehaviorCondition.CreateAlways(), "gskB", TargetPolicy.FirstAlive),
        });
        var registry = new Dictionary<string, BehaviorPatternData> { { "pat_g3", pat } };

        AllyUnit ally = new AllyUnit(MakeAllyData("GA", MakeStats(100, 10, 0, 5)));
        // enemy가 skA, skB 순서 보유 -> 더미면 skA(첫), 실제 AI면 패턴대로 skB
        EnemyUnit enemy = new EnemyUnit(MakeEnemyData("GE", MakeStats(100, 10, 0, 5),
            new List<SkillData> { skA, skB }, patternId: "pat_g3"));

        IntentSystem intent = new IntentSystem();
        EnemyBehaviorSystem sys = new EnemyBehaviorSystem(registry, intent);
        BattleSnapshot snap = new BattleSnapshot(1, new List<AllyUnit> { ally }, new List<EnemyUnit> { enemy });

        sys.DecideAll(snap);
        EnemyIntent reg = intent.GetIntent(enemy);
        Check("G3-1 더미 제거(첫스킬 아닌 패턴결과 skB)", reg != null && reg.Skill.SkillId == "gskB");
        Check("G3-2 대상정책 적용(FirstAlive -> ally)", reg != null && ReferenceEquals(reg.Target, ally));

        // lazy bind: 같은 enemy 재호출 -> 같은 behavior 인스턴스(캐시 히트)
        object behaviors = typeof(EnemyBehaviorSystem)
            .GetField("_behaviors", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sys);
        var dict = (Dictionary<EnemyUnit, IEnemyBehavior>)behaviors;
        IEnemyBehavior first = dict[enemy];
        sys.DecideAll(snap);
        Check("G3-3 lazy bind 캐시 히트(같은 인스턴스)", ReferenceEquals(dict[enemy], first));
        Check("G3-4 잡몹 -> DataDrivenBehavior 바인딩", first is DataDrivenBehavior);

        // 미등록 patternId -> 미등록(무행동)
        EnemyUnit unreg = new EnemyUnit(MakeEnemyData("GUnreg", MakeStats(100, 10, 0, 5),
            new List<SkillData> { skA }, patternId: "no_such_pattern"));
        IntentSystem intent2 = new IntentSystem();
        EnemyBehaviorSystem sys2 = new EnemyBehaviorSystem(registry, intent2);
        sys2.DecideAll(new BattleSnapshot(1, new List<AllyUnit> { ally }, new List<EnemyUnit> { unreg }));
        Check("G3-5 미등록 patternId -> 무등록", intent2.GetIntent(unreg) == null);

        // IsBoss -> 미등록(무행동, G-4 골격만)
        EnemyUnit boss = new EnemyUnit(MakeEnemyData("GBoss", MakeStats(100, 10, 0, 5),
            new List<SkillData> { skA }, patternId: "pat_g3", isBoss: true));
        IntentSystem intent3 = new IntentSystem();
        EnemyBehaviorSystem sys3 = new EnemyBehaviorSystem(registry, intent3);
        sys3.DecideAll(new BattleSnapshot(1, new List<AllyUnit> { ally }, new List<EnemyUnit> { boss }));
        Check("G3-6 IsBoss -> 무등록(실물 미등록)", intent3.GetIntent(boss) == null);

        // 스냅샷 필터: 전투불능 적은 DecideAll에서 스킵
        EnemyUnit downed = new EnemyUnit(MakeEnemyData("GDowned", MakeStats(100, 10, 0, 5),
            new List<SkillData> { skB }, patternId: "pat_g3"));
        downed.SetIncapacitated(true);
        IntentSystem intent4 = new IntentSystem();
        EnemyBehaviorSystem sys4 = new EnemyBehaviorSystem(registry, intent4);
        // 계약 위반(죽은 적 포함) 시에도 방어: LivingEnemies에 넣어도 IsIncapacitated로 스킵
        sys4.DecideAll(new BattleSnapshot(1, new List<AllyUnit> { ally }, new List<EnemyUnit> { downed }));
        Check("G3-7 전투불능 적 스킵", intent4.GetIntent(downed) == null);

        // 결정론: 같은 전장 반복 -> 같은 등록
        IntentSystem intentD = new IntentSystem();
        EnemyBehaviorSystem sysD = new EnemyBehaviorSystem(registry, intentD);
        sysD.DecideAll(snap);
        string first1 = intentD.GetIntent(enemy)?.Skill.SkillId;
        sysD.DecideAll(snap);
        string first2 = intentD.GetIntent(enemy)?.Skill.SkillId;
        Check("G3-8 결정론(반복 등록 동일)", first1 == "gskB" && first1 == first2);

        // BattleFlow Step3 교체: private Step3 리플렉션 호출 -> IntentSystem 등록 확인
        // (더미 로직 제거 후 EnemyBehaviorSystem.DecideAll 호출로 배선되었는지)
        AllyUnit fAlly = new AllyUnit(MakeAllyData("FA", MakeStats(100, 10, 0, 5)));
        EnemyUnit fEnemy = new EnemyUnit(MakeEnemyData("FE", MakeStats(100, 10, 0, 5),
            new List<SkillData> { skA, skB }, patternId: "pat_g3"));
        List<EnemyUnit> fEnemies = new List<EnemyUnit> { fEnemy };
        IntentSystem fIntent = new IntentSystem();
        ProtectionSystem fProt = new ProtectionSystem();
        EnemyBehaviorSystem fBeh = new EnemyBehaviorSystem(registry, fIntent);
        WaveSystem fWave = new WaveSystem(fEnemies,
            new List<IReadOnlyList<EnemyUnit>> { fEnemies }, new List<AllyUnit> { fAlly }, fProt);
        BattleFlowSystem flow = new BattleFlowSystem(
            new List<AllyUnit> { fAlly }, fEnemies, fIntent, fProt, new NoOpExecutor(), fWave, fBeh);

        // _turnNum을 1로 세팅(Step3 스냅샷 turnNum 사용). Step1을 안 거치므로 직접 주입
        SetField(flow, "_turnNum", 1);
        typeof(BattleFlowSystem)
            .GetMethod("Step3_AssignEnemyIntent", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(flow, null);
        EnemyIntent flowReg = fIntent.GetIntent(fEnemy);
        Check("G3-9 BattleFlow.Step3 실제 AI 배선(skB)", flowReg != null && flowReg.Skill.SkillId == "gskB");
    }

    // ============================================================
    // G-4: 보스 스텁 계약·안P 격리·저장 훅
    // ============================================================
    private void RunG4()
    {
        // 계약: BossBehaviorBase 파생 스텁이 IEnemyBehavior로 참조 가능
        TestBossBehavior boss = new TestBossBehavior();
        IEnemyBehavior asContract = boss;
        Check("G4-1 BossBehaviorBase : IEnemyBehavior 계약", asContract != null);

        // 저장 훅 왕복: Restore -> Capture 동일
        boss.RestoreState(3, 7);
        boss.CaptureState(out int ph, out int seq);
        Check("G4-2 저장훅 왕복(phase·step 복원)", ph == 3 && seq == 7);

        // 안P 격리: phaseIndex/sequenceStep이 behavior에만. BattleUnit/EnemyUnit에 범용 필드 없음
        bool unitHasPhase = typeof(BattleUnit)
            .GetField("_phaseIndex", BindingFlags.NonPublic | BindingFlags.Instance) != null
            || typeof(EnemyUnit)
            .GetField("_phaseIndex", BindingFlags.NonPublic | BindingFlags.Instance) != null;
        Check("G4-3 안P 격리(유닛에 phaseIndex 없음)", !unitHasPhase);

        bool behHasPhase = typeof(BossBehaviorBase)
            .GetField("_phaseIndex", BindingFlags.NonPublic | BindingFlags.Instance) != null;
        Check("G4-4 안P 상태는 behavior 내부 보유", behHasPhase);
    }

    // 보스 골격 검증용 최소 파생 스텁(산출물 아님, 프로브 전용). Decide는 첫 스킬 폴백
    private sealed class TestBossBehavior : BossBehaviorBase
    {
        public override EnemyIntent Decide(BattleSnapshot snapshot, EnemyUnit self)
        {
            if (self.Skills.Count == 0) return null;
            AllyUnit target = snapshot.LivingAllies.Count > 0 ? snapshot.LivingAllies[0] : null;
            return new EnemyIntent(self.Skills[0], target);
        }
    }

    // no-op 실행기: Step3만 검증하므로 실행 내부는 비움
    private sealed class NoOpExecutor : IActionExecutor
    {
        public void Execute(ActionCommand command, int currtentTurn) { }
    }

    // ============================================================
    // 리플렉션 조립 헬퍼 (데이터 전부 [SerializeField] private + 세터 없음)
    // ============================================================
    private static void SetField(object target, string field, object value)
    {
        FieldInfo f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) throw new Exception($"필드 없음: {field} in {target.GetType().Name}");
        f.SetValue(target, value);
    }

    private static UnitStats MakeStats(int hp, int atk, int def, int spd)
    {
        object boxed = new UnitStats();
        SetField(boxed, "_maxHP", hp);
        SetField(boxed, "_attack", atk);
        SetField(boxed, "_defense", def);
        SetField(boxed, "_speed", spd);
        return (UnitStats)boxed;
    }

    private static SkillData MakeSkill(string id, TargetRule rule)
    {
        SkillData s = ScriptableObject.CreateInstance<SkillData>();
        SetField(s, "_skillId", id);
        SetField(s, "_displayName", id);
        SetField(s, "_apCost", 0);
        SetField(s, "_damageCoeffi", 0f);
        SetField(s, "_fixedDamage", 0);
        SetField(s, "_healingCoeffi", 0f);
        SetField(s, "_fixedHealing", 0);
        SetField(s, "_shieldCoeffi", 0f);
        SetField(s, "_fixedShield", 0);
        SetField(s, "_breakAmount", 0);
        SetField(s, "_direction", AttackDirection.None);
        SetField(s, "_targetRule", rule);
        SetField(s, "_effects", new List<StatusEffectData>());
        SetField(s, "_cleansesNormalStatus", false);
        SetField(s, "_isUnavoidable", false);
        return s;
    }

    private static AllyUnitData MakeAllyData(string id, UnitStats stats)
    {
        AllyUnitData d = ScriptableObject.CreateInstance<AllyUnitData>();
        SetField(d, "_unitId", id);
        SetField(d, "_displayName", id);
        SetField(d, "_baseStats", stats);
        SetField(d, "_equippedSkills", new List<SkillData>());
        SetField(d, "_availableSkills", new List<SkillData>());
        return d;
    }

    private static EnemyUnitData MakeEnemyData(string id, UnitStats stats,
        List<SkillData> skills = null, string patternId = "", bool isBoss = false)
    {
        EnemyUnitData d = ScriptableObject.CreateInstance<EnemyUnitData>();
        SetField(d, "_enemyId", id);
        SetField(d, "_displayName", id);
        SetField(d, "_isBoss", isBoss);
        SetField(d, "_maxBreakGauge", 100);
        SetField(d, "_maxCrackGauge", 100);
        SetField(d, "_skills", skills ?? new List<SkillData>());
        SetField(d, "_statusImmunities", new List<StatusEffectData>());
        SetField(d, "_behaviorPatternId", patternId);
        SetField(d, "_baseStats", stats);
        return d;
    }

    private static BehaviorPatternData MakePattern(string id, List<BehaviorRule> rules)
    {
        BehaviorPatternData p = ScriptableObject.CreateInstance<BehaviorPatternData>();
        SetField(p, "_patternId", id);
        SetField(p, "_rules", rules);
        return p;
    }
}