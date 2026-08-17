using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// F 대주제 통합 프로브. 커밋 제외(_Scratch, asmdef 밖). 빈 GameObject에 붙여 Play -> Console 확인
// 데이터는 전부 [SerializeField] private + 세터 없음 -> 리플렉션으로 주입
public sealed class F_IntegrationProbe : MonoBehaviour
{
    private int _pass;
    private int _fail;

    private void Start()
    {
        RunF1();
        RunF2();
        RunF3();
        Debug.Log($"=====[F 통합 프로브] PASS {_pass} / FAIL {_fail}=====");
    }

    private void Check(string name, bool cond)
    {
        if (cond) { _pass++; Debug.Log($"[PASS] {name}"); }
        else { _fail++; Debug.LogError($"[FAIL] {name}"); }
    }

    // ===================== F-1: 전투불능 전이 ===================== //
    private void RunF1()
    {
        // F1-1: 파이프 피해로 HP0 -> 즉시 전투불능 전이(핸들러 단일화)
        {
            AllyUnit ally = new AllyUnit(MakeAllyData("A1", MakeStats(100, 50, 0, 10)));
            EnemyUnit enemy = new EnemyUnit(MakeEnemyData("E1", MakeStats(100, 10, 0, 5)));
            enemy.ModifyHP(-90);   // HP 10
            SkillData hit = MakeSkill("hit", fixedDmg: 9999, dir: AttackDirection.Mid);
            ProtectionSystem prot = new ProtectionSystem();
            IntentSystem intent = new IntentSystem();
            ActionResolver resolver = new ActionResolver(
                new List<AllyUnit> { ally }, new List<EnemyUnit> { enemy }, prot, intent);

            resolver.Execute(ActionCommand.CreateSkill(ally, hit, enemy), 1);
            Check("F1-1 파이프 HP0 -> 즉시 전투불능", enemy.CurrHP == 0 && enemy.IsIncapacitated);
        }

        // F1-2: 같은 턴 안에서 방금 죽은 대상을 뒤 순번이 재타격하지 않음(F-1이 고친 버그의 회귀)
        {
            AllyUnit ally = new AllyUnit(MakeAllyData("A1", MakeStats(10, 10, 0, 1)));   // HP10, 최저속
            SkillData kill = MakeSkill("kill", fixedDmg: 9999, dir: AttackDirection.Mid);
            EnemyUnit fast = new EnemyUnit(MakeEnemyData("Efast", MakeStats(100, 50, 0, 20), new List<SkillData> { kill }));
            EnemyUnit slow = new EnemyUnit(MakeEnemyData("Eslow", MakeStats(100, 50, 0, 15), new List<SkillData> { kill }));

            List<AllyUnit> allies = new List<AllyUnit> { ally };
            List<EnemyUnit> enemies = new List<EnemyUnit> { fast, slow };
            ProtectionSystem prot = new ProtectionSystem();
            IntentSystem intent = new IntentSystem();
            ActionResolver resolver = new ActionResolver(allies, enemies, prot, intent);
            WaveSystem wave = new WaveSystem(enemies, new List<IReadOnlyList<EnemyUnit>> { enemies }, allies, prot);
            BattleFlowSystem flow = new BattleFlowSystem(allies, enemies, intent, prot, resolver, wave);

            flow.ExecuteTurn();
            // fast(속20)가 ally 죽임 -> slow(속15)는 대상상실로 스킵 -> 미행동
            Check("F1-2 죽은 대상 재타격 방지",
                ally.IsIncapacitated && fast.ActedThisTurn && !slow.ActedThisTurn);
        }

        // F1-3: 8단계 지속피해 틱으로 HP0 -> 전투불능 전이
        {
            EnemyUnit caster = new EnemyUnit(MakeEnemyData("caster", MakeStats(100, 20, 0, 10)));  // 스킬 없음 -> 행동 안 함
            AllyUnit ally = new AllyUnit(MakeAllyData("A1", MakeStats(100, 10, 0, 5)));
            StatusEffectData burn = MakeStatus("burn", StatusEffectCategory.Normal, 5,
                new List<EffectComponent> { MakeComponent(EffectKind.DamageOverTime, 1.0f) });  // 스냅샷 = 20 * 1.0 = 20
            StatusEffectSystem.Apply(ally, burn, caster, 0);   // 적용턴 0
            ally.ModifyHP(-85);   // HP 15 (< 스냅샷 20)

            List<AllyUnit> allies = new List<AllyUnit> { ally };
            List<EnemyUnit> enemies = new List<EnemyUnit> { caster };
            ProtectionSystem prot = new ProtectionSystem();
            IntentSystem intent = new IntentSystem();
            ActionResolver resolver = new ActionResolver(allies, enemies, prot, intent);
            WaveSystem wave = new WaveSystem(enemies, new List<IReadOnlyList<EnemyUnit>> { enemies }, allies, prot);
            BattleFlowSystem flow = new BattleFlowSystem(allies, enemies, intent, prot, resolver, wave);

            flow.ExecuteTurn();   // turnNum 1, Step8 틱(1>0) -> HP 15-20 -> 0 -> 전이
            Check("F1-3 틱 HP0 -> 전투불능", ally.IsIncapacitated);
        }

        // F1-4: 멱등 — 이미 전투불능인 유닛 재호출 시 false(중복 전이 없음)
        {
            EnemyUnit u = new EnemyUnit(MakeEnemyData("u", MakeStats(100, 10, 0, 5)));
            u.ModifyHP(-100);   // HP0
            u.SetIncapacitated(true);
            bool r = IncapacitationSystem.CheckAndTransition(u);
            Check("F1-4 멱등(재호출 false)", !r && u.IsIncapacitated);
        }

        // F1-5: HP0 도달 시 붕괴 누적 스킵(사망 동시 발생 X)
        {
            AllyUnit ally = new AllyUnit(MakeAllyData("A", MakeStats(100, 50, 0, 10)));
            EnemyUnit enemy = new EnemyUnit(MakeEnemyData("E", MakeStats(100, 10, 0, 5)));
            enemy.ModifyHP(-90);   // HP10
            SkillData brk = MakeSkill("brk", fixedDmg: 9999, breakAmount: 50, dir: AttackDirection.Mid);
            ProtectionSystem prot = new ProtectionSystem();
            IntentSystem intent = new IntentSystem();
            ActionResolver resolver = new ActionResolver(
                new List<AllyUnit> { ally }, new List<EnemyUnit> { enemy }, prot, intent);

            resolver.Execute(ActionCommand.CreateSkill(ally, brk, enemy), 1);
            Check("F1-5 HP0 시 붕괴 누적 스킵",
                enemy.IsIncapacitated && enemy.CurrBreakOrCrackGauge == 0);
        }
    }

    // ===================== F-2: WaveSystem 전환 시퀀스 ===================== //
    private void RunF2()
    {
        // F2-6: 전투상태 전원 소거(보호막·회피·방향방어·보호·버프)
        {
            AllyUnit a1 = new AllyUnit(MakeAllyData("A1", MakeStats(100, 20, 10, 10)));
            AllyUnit a2 = new AllyUnit(MakeAllyData("A2", MakeStats(100, 20, 10, 10)));
            List<AllyUnit> allies = new List<AllyUnit> { a1, a2 };
            EnemyUnit ew1 = new EnemyUnit(MakeEnemyData("Ew1", MakeStats(50, 10, 0, 5)));
            List<EnemyUnit> active = new List<EnemyUnit> { new EnemyUnit(MakeEnemyData("Ew0", MakeStats(50, 10, 0, 5))) };
            List<IReadOnlyList<EnemyUnit>> waves = new List<IReadOnlyList<EnemyUnit>>
                { new List<EnemyUnit>(active), new List<EnemyUnit> { ew1 } };
            ProtectionSystem prot = new ProtectionSystem();
            WaveSystem wave = new WaveSystem(active, waves, allies, prot);

            ShieldSystem.Grant(a1, 30);
            EvasionSystem.Grant(a1, 2);
            a1.SetStance(AttackDirection.High, AttackDirection.None);
            prot.SetProtect(a2, a1);
            StatusEffectData buff = MakeStatus("defbuff", StatusEffectCategory.Buff, 3,
                new List<EffectComponent> { MakeComponent(EffectKind.DefenseMod, 5f) });
            StatusEffectSystem.Apply(a1, buff, a1, 0);

            wave.AdvanceToNextWave();
            Check("F2-6 보호막 소거", a1.Shield == 0);
            Check("F2-6 회피 소거", a1.EvasionCount == 0);
            Check("F2-6 방향방어 소거", a1.DefenseDirection == AttackDirection.None);
            Check("F2-6 보호 소거", prot.GetProtector(a1) == null);
            Check("F2-6 버프 소거", a1.FindStatusEffect("defbuff") == null);
        }

        // F2-7: 전투불능 아군 HP1 복귀 + AP 유지
        {
            AllyUnit a1 = new AllyUnit(MakeAllyData("A1", MakeStats(100, 20, 10, 10)));
            a1.SetAP(5);
            a1.ModifyHP(-100);
            a1.SetIncapacitated(true);
            List<AllyUnit> allies = new List<AllyUnit> { a1 };
            List<EnemyUnit> active = new List<EnemyUnit> { new EnemyUnit(MakeEnemyData("Ew0", MakeStats(50, 10, 0, 5))) };
            List<IReadOnlyList<EnemyUnit>> waves = new List<IReadOnlyList<EnemyUnit>>
                { new List<EnemyUnit>(active), new List<EnemyUnit> { new EnemyUnit(MakeEnemyData("Ew1", MakeStats(50, 10, 0, 5))) } };
            WaveSystem wave = new WaveSystem(active, waves, allies, new ProtectionSystem());

            wave.AdvanceToNextWave();
            Check("F2-7 HP1 복귀 + 플래그 해제", a1.CurrHP == 1 && !a1.IsIncapacitated);
            Check("F2-7 AP 유지", a1.CurrAP == 5);
        }

        // F2-8: 복귀자 Normal 제거 vs 생존자 Normal 유지(비대칭)
        {
            AllyUnit downed = new AllyUnit(MakeAllyData("Ad", MakeStats(100, 20, 10, 10)));
            AllyUnit alive = new AllyUnit(MakeAllyData("Aa", MakeStats(100, 20, 10, 10)));
            downed.ModifyHP(-100);
            downed.SetIncapacitated(true);
            StatusEffectData burn = MakeStatus("burn", StatusEffectCategory.Normal, 3,
                new List<EffectComponent> { MakeComponent(EffectKind.DamageOverTime, 1.0f) });
            StatusEffectSystem.Apply(downed, burn, downed, 0);
            StatusEffectSystem.Apply(alive, burn, alive, 0);

            List<AllyUnit> allies = new List<AllyUnit> { downed, alive };
            List<EnemyUnit> active = new List<EnemyUnit> { new EnemyUnit(MakeEnemyData("Ew0", MakeStats(50, 10, 0, 5))) };
            List<IReadOnlyList<EnemyUnit>> waves = new List<IReadOnlyList<EnemyUnit>>
                { new List<EnemyUnit>(active), new List<EnemyUnit> { new EnemyUnit(MakeEnemyData("Ew1", MakeStats(50, 10, 0, 5))) } };
            WaveSystem wave = new WaveSystem(active, waves, allies, new ProtectionSystem());

            wave.AdvanceToNextWave();
            Check("F2-8 복귀자 Normal 제거", downed.FindStatusEffect("burn") == null);
            Check("F2-8 생존자 Normal 유지", alive.FindStatusEffect("burn") != null);
        }

        // F2-9: 복귀자 Buff는 전환순서 5(ClearBuff 전원)에서 이미 제거
        {
            AllyUnit a1 = new AllyUnit(MakeAllyData("A1", MakeStats(100, 20, 10, 10)));
            a1.ModifyHP(-100);
            a1.SetIncapacitated(true);
            StatusEffectData buff = MakeStatus("spdbuff", StatusEffectCategory.Buff, 3,
                new List<EffectComponent> { MakeComponent(EffectKind.SpeedMod, 5f) });
            StatusEffectSystem.Apply(a1, buff, a1, 0);
            List<AllyUnit> allies = new List<AllyUnit> { a1 };
            List<EnemyUnit> active = new List<EnemyUnit> { new EnemyUnit(MakeEnemyData("Ew0", MakeStats(50, 10, 0, 5))) };
            List<IReadOnlyList<EnemyUnit>> waves = new List<IReadOnlyList<EnemyUnit>>
                { new List<EnemyUnit>(active), new List<EnemyUnit> { new EnemyUnit(MakeEnemyData("Ew1", MakeStats(50, 10, 0, 5))) } };
            WaveSystem wave = new WaveSystem(active, waves, allies, new ProtectionSystem());

            wave.AdvanceToNextWave();
            Check("F2-9 복귀자 Buff 제거", a1.FindStatusEffect("spdbuff") == null);
        }

        // F2-10: 활성 적 리스트 교체가 같은 인스턴스로 전파(BattleFlow 공유)
        {
            AllyUnit a1 = new AllyUnit(MakeAllyData("A1", MakeStats(100, 20, 10, 10)));
            List<AllyUnit> allies = new List<AllyUnit> { a1 };
            EnemyUnit ew1 = new EnemyUnit(MakeEnemyData("Ew1", MakeStats(50, 10, 0, 5)));
            List<EnemyUnit> active = new List<EnemyUnit> { new EnemyUnit(MakeEnemyData("Ew0", MakeStats(50, 10, 0, 5))) };
            List<IReadOnlyList<EnemyUnit>> waves = new List<IReadOnlyList<EnemyUnit>>
                { new List<EnemyUnit>(active), new List<EnemyUnit> { ew1 } };
            ProtectionSystem prot = new ProtectionSystem();
            IntentSystem intent = new IntentSystem();
            ActionResolver resolver = new ActionResolver(allies, active, prot, intent);
            WaveSystem wave = new WaveSystem(active, waves, allies, prot);
            BattleFlowSystem flow = new BattleFlowSystem(allies, active, intent, prot, resolver, wave);

            wave.AdvanceToNextWave();
            // BattleFlow._enemies가 active와 같은 인스턴스인지 + 내용이 웨이브1로 교체됐는지
            object flowEnemies = typeof(BattleFlowSystem)
                .GetField("_enemies", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(flow);
            Check("F2-10 적 교체 전파",
                ReferenceEquals(flowEnemies, active) && active.Count == 1 && ReferenceEquals(active[0], ew1));
        }

        // F2-11: 마지막 웨이브에서 HasNextWave false + 재호출 예외
        {
            AllyUnit a1 = new AllyUnit(MakeAllyData("A1", MakeStats(100, 20, 10, 10)));
            List<AllyUnit> allies = new List<AllyUnit> { a1 };
            List<EnemyUnit> active = new List<EnemyUnit> { new EnemyUnit(MakeEnemyData("Ew0", MakeStats(50, 10, 0, 5))) };
            List<IReadOnlyList<EnemyUnit>> waves = new List<IReadOnlyList<EnemyUnit>>
                { new List<EnemyUnit>(active), new List<EnemyUnit> { new EnemyUnit(MakeEnemyData("Ew1", MakeStats(50, 10, 0, 5))) } };
            WaveSystem wave = new WaveSystem(active, waves, allies, new ProtectionSystem());

            wave.AdvanceToNextWave();   // -> 웨이브1(마지막)
            bool threw = false;
            try { wave.AdvanceToNextWave(); }
            catch (InvalidOperationException) { threw = true; }
            Check("F2-11 마지막 HasNextWave false", !wave.HasNextWave);
            Check("F2-11 재호출 예외", threw);
        }
    }

    // ===================== F-3: 9단계 판정 ===================== //
    private void RunF3()
    {
        // F3-12: 전 아군 전투불능 -> Defeat
        {
            BattleFlowSystem flow = MakeFlow(out AllyUnit a, out EnemyUnit e, twoWaves: true);
            a.ModifyHP(-100); a.SetIncapacitated(true);   // 전 아군 전투불능
            Check("F3-12 전멸 -> Defeat", Judge(flow) == BattleOutcome.Defeat);
        }

        // F3-13: 아군·적 동시 전멸 -> 전멸 먼저라 Defeat
        {
            BattleFlowSystem flow = MakeFlow(out AllyUnit a, out EnemyUnit e, twoWaves: true);
            a.ModifyHP(-100); a.SetIncapacitated(true);
            e.ModifyHP(-100); e.SetIncapacitated(true);
            Check("F3-13 동시 HP0 -> Defeat", Judge(flow) == BattleOutcome.Defeat);
        }

        // F3-14: 현 웨이브 적 전멸 + 남은 웨이브 -> Ongoing + 실제 전환
        {
            List<AllyUnit> allies = new List<AllyUnit> { new AllyUnit(MakeAllyData("A", MakeStats(100, 20, 10, 10))) };
            EnemyUnit ew1 = new EnemyUnit(MakeEnemyData("Ew1", MakeStats(50, 10, 0, 5)));
            EnemyUnit ew0 = new EnemyUnit(MakeEnemyData("Ew0", MakeStats(50, 10, 0, 5)));
            List<EnemyUnit> active = new List<EnemyUnit> { ew0 };
            List<IReadOnlyList<EnemyUnit>> waves = new List<IReadOnlyList<EnemyUnit>>
                { new List<EnemyUnit>(active), new List<EnemyUnit> { ew1 } };
            ProtectionSystem prot = new ProtectionSystem();
            IntentSystem intent = new IntentSystem();
            ActionResolver resolver = new ActionResolver(allies, active, prot, intent);
            WaveSystem wave = new WaveSystem(active, waves, allies, prot);
            BattleFlowSystem flow = new BattleFlowSystem(allies, active, intent, prot, resolver, wave);

            ew0.ModifyHP(-100); ew0.SetIncapacitated(true);   // 현 웨이브 적 전멸
            BattleOutcome outcome = Judge(flow);
            Check("F3-14 웨이브전환 -> Ongoing + 적 교체",
                outcome == BattleOutcome.Ongoing && active.Count == 1 && ReferenceEquals(active[0], ew1));
        }

        // F3-15: 마지막 웨이브 적 전멸 -> Victory
        {
            BattleFlowSystem flow = MakeFlow(out AllyUnit a, out EnemyUnit e, twoWaves: false);
            e.ModifyHP(-100); e.SetIncapacitated(true);
            Check("F3-15 마지막 웨이브 -> Victory", Judge(flow) == BattleOutcome.Victory);
        }

        // F3-16: 양쪽 생존 -> Ongoing
        {
            BattleFlowSystem flow = MakeFlow(out AllyUnit a, out EnemyUnit e, twoWaves: true);
            Check("F3-16 양쪽 생존 -> Ongoing", Judge(flow) == BattleOutcome.Ongoing);
        }

        // F3-17: 페이즈 전환 스텁은 항상 false
        {
            BattleFlowSystem flow = MakeFlow(out AllyUnit a, out EnemyUnit e, twoWaves: true);
            bool phase = (bool)typeof(BattleFlowSystem)
                .GetMethod("CheckPhaseTransition", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(flow, null);
            Check("F3-17 페이즈 스텁 false", !phase);
        }
    }

    // 아군1 + 적1 최소 전투 셋업. twoWaves면 다음 웨이브 존재
    private BattleFlowSystem MakeFlow(out AllyUnit ally, out EnemyUnit enemy, bool twoWaves)
    {
        ally = new AllyUnit(MakeAllyData("A", MakeStats(100, 20, 10, 10)));
        enemy = new EnemyUnit(MakeEnemyData("E", MakeStats(50, 10, 0, 5)));
        List<AllyUnit> allies = new List<AllyUnit> { ally };
        List<EnemyUnit> active = new List<EnemyUnit> { enemy };
        List<IReadOnlyList<EnemyUnit>> waves = twoWaves
            ? new List<IReadOnlyList<EnemyUnit>> { new List<EnemyUnit>(active), new List<EnemyUnit> { new EnemyUnit(MakeEnemyData("E2", MakeStats(50, 10, 0, 5))) } }
            : new List<IReadOnlyList<EnemyUnit>> { new List<EnemyUnit>(active) };
        ProtectionSystem prot = new ProtectionSystem();
        IntentSystem intent = new IntentSystem();
        ActionResolver resolver = new ActionResolver(allies, active, prot, intent);
        WaveSystem wave = new WaveSystem(active, waves, allies, prot);
        return new BattleFlowSystem(allies, active, intent, prot, resolver, wave);
    }

    private static BattleOutcome Judge(BattleFlowSystem flow)
        => (BattleOutcome)typeof(BattleFlowSystem)
            .GetMethod("Step9_Judge", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(flow, null);

    // ===================== 리플렉션 데이터 팩토리 ===================== //
    private static void SetField(object target, string field, object value)
    {
        FieldInfo f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) throw new Exception($"필드 없음: {field} in {target.GetType().Name}");
        f.SetValue(target, value);
    }

    private static UnitStats MakeStats(int hp, int atk, int def, int spd)
    {
        object boxed = new UnitStats();   // struct -> boxing 후 필드 주입
        SetField(boxed, "_maxHP", hp);
        SetField(boxed, "_attack", atk);
        SetField(boxed, "_defense", def);
        SetField(boxed, "_speed", spd);
        return (UnitStats)boxed;
    }

    private static EffectComponent MakeComponent(EffectKind kind, float mag)
    {
        object boxed = new EffectComponent();
        SetField(boxed, "_effectKind", kind);
        SetField(boxed, "_magnitude", mag);
        return (EffectComponent)boxed;
    }

    private static SkillData MakeSkill(string id, float dmgCoeffi = 0f, int fixedDmg = 0,
        int breakAmount = 0, AttackDirection dir = AttackDirection.None, TargetRule rule = TargetRule.Single)
    {
        SkillData s = ScriptableObject.CreateInstance<SkillData>();
        SetField(s, "_skillId", id);
        SetField(s, "_displayName", id);
        SetField(s, "_apCost", 0);
        SetField(s, "_damageCoeffi", dmgCoeffi);
        SetField(s, "_fixedDamage", fixedDmg);
        SetField(s, "_healingCoeffi", 0f);
        SetField(s, "_fixedHealing", 0);
        SetField(s, "_shieldCoeffi", 0f);
        SetField(s, "_fixedShield", 0);
        SetField(s, "_breakAmount", breakAmount);
        SetField(s, "_direction", dir);
        SetField(s, "_targetRule", rule);
        SetField(s, "_effects", new List<StatusEffectData>());
        SetField(s, "_cleansesNormalStatus", false);
        SetField(s, "_isUnavoidable", false);
        return s;
    }

    private static StatusEffectData MakeStatus(string id, StatusEffectCategory cat, int duration, List<EffectComponent> comps)
    {
        StatusEffectData s = ScriptableObject.CreateInstance<StatusEffectData>();
        SetField(s, "_statusId", id);
        SetField(s, "_displayName", id);
        SetField(s, "_category", cat);
        SetField(s, "_baseDuration", duration);
        SetField(s, "_components", comps);
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

    private static EnemyUnitData MakeEnemyData(string id, UnitStats stats, List<SkillData> skills = null)
    {
        EnemyUnitData d = ScriptableObject.CreateInstance<EnemyUnitData>();
        SetField(d, "_enemyId", id);
        SetField(d, "_displayName", id);
        SetField(d, "_isBoss", false);
        SetField(d, "_maxBreakGauge", 100);
        SetField(d, "_maxCrackGauge", 100);
        SetField(d, "_skills", skills ?? new List<SkillData>());
        SetField(d, "_statusImmunities", new List<StatusEffectData>());
        SetField(d, "_behaviorPatternId", "");
        SetField(d, "_baseStats", stats);
        return d;
    }
}