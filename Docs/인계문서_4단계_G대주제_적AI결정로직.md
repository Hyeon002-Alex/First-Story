# 인계문서 — 4단계 G대주제: 적 AI 결정 로직

- **상태**: `완료` (통합 프로브 통과 → 코드 리뷰 → 리뷰 반영 → 본 문서. 4순차 정규 종료)
- **작성 기준**: 리뷰 반영 후 최종 코드
- **선행 대주제**: A(데이터·유닛), B(AP·명령서·intent·턴루프), C(한방 실행 파이프), F(웨이브·전투불능·승패)
- **핵심 한 줄**: B단계에서 심은 더미 intent(첫 스킬 + 첫 생존 아군)를 실제 `Decide` 결과로 교체. intent 모양(`EnemyIntent{Skill, Target}`) 불변이라 파이프·IntentSystem은 무수정.

---

## ① 산출물

### 신규 11파일

**Data 레이어 (5)**
- `Data/Enums/ConditionKind.cs` — 조건 종류 enum(5종)
- `Data/Enums/TargetPolicy.cs` — 단일 대상 선택 정책 enum(4종)
- `Data/BehaviorCondition.cs` — 조건 struct + 팩토리 5 + `Evaluate`(정수 교차곱)
- `Data/BehaviorRule.cs` — 규칙 1줄(조건 + skillId + 대상정책)
- `Data/BehaviorPatternData.cs` — 잡몹 패턴 SO(규칙 목록)

**Systems 레이어 (6)**
- `Systems/BattleSnapshot.cs` — 전장 스냅샷 struct(turnNum + 생존 아군·적 목록)
- `Systems/IEnemyBehavior.cs` — 결정 계약(`Decide → EnemyIntent`)
- `Systems/TargetSelectionPolicy.cs` — 대상 선택 static 헬퍼(tie-break 소유)
- `Systems/DataDrivenBehavior.cs` — 잡몹 결정 구현(stateless, 첫매치)
- `Systems/EnemyBehaviorSystem.cs` — 조율(팩토리 lazy bind + `DecideAll` → IntentSystem)
- `Systems/BossBehaviorBase.cs` — 보스 골격 abstract(안P 상태 + 저장 훅 자리)

### 기존 수정 1파일
- `Systems/BattleFlowSystem.cs` — 필드 `_behaviorSystem` 추가 / 생성자 파라미터 `behaviorSystem` 추가 / `Step3_AssignEnemyIntent` 더미 제거 후 스냅샷 조립 + `DecideAll` 호출로 교체

### 커밋 제외 (산출물 아님)
- `_Scratch/G_IntegrationProbe.cs` — 통합 프로브 39 checks. asmdef 밖, 커밋 제외

---

## ② 핵심 구조

데이터(정적 규칙)와 로직(결정 실행)을 레이어로 분리했다. asmdef 단방향(`Systems → Core → Data`)이 이 분리를 컴파일 타임에 강제한다.

```
[Data]  BehaviorCondition(struct) ─┐
                                    ├─> BehaviorRule ──> BehaviorPatternData(SO)
        ConditionKind / TargetPolicy(enum)
                                              │ (patternId 문자열 키로 레지스트리 조회)
[Systems]                                     ▼
        EnemyBehaviorSystem ──(팩토리 lazy bind)──> IEnemyBehavior
              │                                        ├─ DataDrivenBehavior (잡몹, stateless)
              │                                        └─ BossBehaviorBase 파생 (보스, stateful) [골격만]
              │ DecideAll(snapshot)
              ▼
        IntentSystem.SetIntent  ← BattleFlowSystem.Step3에서 호출
```

**결정 흐름 (한 턴)**: `BattleFlowSystem.Step3` → 생존 필터로 `BattleSnapshot` 조립 → `EnemyBehaviorSystem.DecideAll` → 적별 `GetOrBind`(캐시 미스면 팩토리) → `behavior.Decide` → 규칙 위→아래 첫 매치 → `skillId` 해석 + 대상정책 → `EnemyIntent` → `IntentSystem.SetIntent`.

**AI 대상정책 vs 실행 파이프 역할 분리 (중요)**: AI는 `intent.Target`으로 실릴 "예정 대상 1명"만 정한다. 스킬 형태 확장(Area), 보호 리다이렉트, 고정 대상 면역은 전부 실행 시점 `TargetingSystem`(D 대주제) 소유다. AI와 파이프가 대상을 이중으로 안 건드린다.

---

## ③ 공개 인터페이스

```csharp
// Data
enum ConditionKind { Always, SelfHpBelow, TurnNumberMod, TurnNumberAtLeast, SurvivingAllyAtLeast }
enum TargetPolicy  { FirstAlive, LowestHp, HighestHp, HighestAttack }

struct BehaviorCondition
    static BehaviorCondition CreateAlways()
    static BehaviorCondition CreateSelfHpBelow(float ratio)
    static BehaviorCondition CreateTurnNumberMod(int divisor, int remainder)
    static BehaviorCondition CreateTurnNumberAtLeast(int turn)
    static BehaviorCondition CreateSurvivingAllyAtLeast(int count)
    ConditionKind Kind { get; }
    bool Evaluate(int currHp, int maxHp, int turnNum, int livingAllyCount)   // 정수 교차곱

sealed class BehaviorRule
    BehaviorRule()                                                          // Unity 역직렬화용
    BehaviorRule(BehaviorCondition condition, string skillId, TargetPolicy targetPolicy)
    BehaviorCondition Condition { get; }  string SkillId { get; }  TargetPolicy TargetPolicy { get; }

sealed class BehaviorPatternData : ScriptableObject
    string PatternId { get; }  IReadOnlyList<BehaviorRule> Rules { get; }

// Systems
readonly struct BattleSnapshot
    BattleSnapshot(int turnNum, IReadOnlyList<AllyUnit> livingAllies, IReadOnlyList<EnemyUnit> livingEnemies)
    int TurnNum { get; }  IReadOnlyList<AllyUnit> LivingAllies { get; }  IReadOnlyList<EnemyUnit> LivingEnemies { get; }

interface IEnemyBehavior
    EnemyIntent Decide(BattleSnapshot snapshot, EnemyUnit self)             // 결정 불가 시 null

static class TargetSelectionPolicy
    static AllyUnit Select(TargetPolicy policy, IReadOnlyList<AllyUnit> candidates)   // 후보 없음 시 null

sealed class DataDrivenBehavior : IEnemyBehavior
    DataDrivenBehavior(BehaviorPatternData pattern)

sealed class EnemyBehaviorSystem
    EnemyBehaviorSystem(IReadOnlyDictionary<string, BehaviorPatternData> patternRegistry, IntentSystem intentSystem)
    void DecideAll(BattleSnapshot snapshot)

abstract class BossBehaviorBase : IEnemyBehavior
    abstract EnemyIntent Decide(BattleSnapshot snapshot, EnemyUnit self)
    protected virtual bool ShouldAdvancePhase(BattleSnapshot snapshot, EnemyUnit self)   // 기본 false
    virtual void CaptureState(out int phaseIndex, out int sequenceStep)     // 저장 훅
    virtual void RestoreState(int phaseIndex, int sequenceStep)
    protected int _phaseIndex, _sequenceStep

// 수정된 배선
class BattleFlowSystem
    // 생성자 끝에 EnemyBehaviorSystem behaviorSystem 추가
    BattleFlowSystem(IReadOnlyList<AllyUnit> allies, List<EnemyUnit> enemies, IntentSystem intentSystem,
                     ProtectionSystem protection, IActionExecutor executor, WaveSystem waveSystem,
                     EnemyBehaviorSystem behaviorSystem)
```

**소비하는 기존 인터페이스**: `EnemyUnit.BehaviorPatternID`(끝 대문자 D — 코드 SSOT 명명), `EnemyUnit.Skills`, `EnemyUnit.IsBoss`, `EnemyUnit.EnemyId`, `BattleUnit.CurrHP/MaxHP/EffectiveAttack/IsIncapacitated`, `SkillData.SkillId/TargetRule`, `IntentSystem.SetIntent/GetIntent/ClearAll`, `EnemyIntent(SkillData, BattleUnit)`.

---

## ④ 의존성

- **레이어 배치**: 조건·규칙·패턴·enum = Data / 계약·구현·조율·스냅샷·정책·보스골격 = Systems. Data는 `UnityEngine`·`System`만 참조(Core·Systems 미참조 = 단방향 준수).
- **참조 대주제**: A(EnemyUnit·SkillData·EnemyUnitData), B/C(EnemyIntent·IntentSystem), F(BattleFlowSystem·WaveSystem·ProtectionSystem 조립 맥락). D의 `TargetingSystem`은 실행측이라 G가 직접 참조하지 않음(역할 분리).
- **주입 관계**: 셋업/조립부가 `Dictionary<string, BehaviorPatternData>` 레지스트리 + `IntentSystem`을 `EnemyBehaviorSystem`에 주입 → `EnemyBehaviorSystem`을 `BattleFlowSystem` 생성자에 주입.
- **DG-1 강제 사항**: `BehaviorCondition.Evaluate`가 `EnemyUnit`(Core)을 못 받는다(Data→Core 역참조 금지). 그래서 primitive(currHp·maxHp·turnNum·livingAllyCount)만 받고, 전장→primitive 추출은 `DataDrivenBehavior`(Systems)가 수행.

---

## ⑤ 검증 결과 + 리뷰 결과·반영

### 통합 프로브: **PASS 39 / FAIL 0**
- G-1(13): Evaluate 5종 kind + 경계(미만) + 나눗수 0 방어 + 결정론 + Rules 순서 보존
- G-2(13): 첫매치·폴백·미보유 skillId 스킵 / 대상정책 4종 + tie-break 슬롯앞(엄격 부등호) / Self→self / 대상전멸→null / 진영 뒤집힘 / 결정론
- G-3(9): 더미 제거(첫스킬 아님) + 대상정책 배선 / lazy bind 캐시 히트 / 잡몹·미등록·보스 분기 / 전투불능 적 스킵 / 결정론 / BattleFlow.Step3 실제 배선
- G-4(4): IEnemyBehavior 계약 / 저장 훅 왕복 / 안P 격리(유닛에 phaseIndex 없음) / 상태는 behavior 내부

**프로브가 실제로 잡은 버그 1건**: G2-12(진영 뒤집힘). 구현 중 `livingAllyCount`를 `LivingEnemies` 대신 `LivingAllies`로 참조 → 첫 규칙 실패로 폴백. 프로브가 포착해 수정. 검증 체계가 제 역할을 한 사례.

### 코드 리뷰: 높음 1 / 중간 2 / 낮음 6

**[높음/자기위반 → 반영 완료]** `SelfHpBelow`가 float 나눗셈(`CurrHP/(float)MaxHP`)으로 HP 비율을 계산 → 계산 정밀도 규율("float 표현 의존 계산 금지") 위반. `Evaluate` 서명을 `float selfHpRatio` → `int currHp, int maxHp`로 바꾸고 정수 교차곱 `currHp × 100 < Round(threshold × 100) × maxHp`로 반영. 반올림 규약은 `CombatCalculator.ToScaled`와 동일(`Math.Round(..., MidpointRounding.AwayFromZero)`)로 계산식 일관성 유지. 전환 후 프로브 39/0 회귀 확인(경계 G1-2/3/4, 첫매치 G2-1/2 동일 결과).

**[중간 → 인계 명시]** ①`livingAllyCount` 자신 포함 여부 미확정(현재 `LivingEnemies.Count`가 자신 포함, "나 말고"면 -1 — GPT 스펙 대기). ②`BossBehaviorBase` 저장 훅 미배선(save/load 시스템 부재).

**[낮음 → 수용·기록]** 잡몹도 적별 behavior 인스턴스(설계 §7 통일 선택, 무해) / 죽은 적 캐시 잔류(전투 종료 시 폐기) / 스냅샷 매 턴 List 2개 할당(헤드리스라 무해) / `Debug` 전역 로거 의존 / `ShouldAdvancePhase` 현재 호출처 없음(D10 자리) / 폴백 스킬마저 미보유 시 null 무행동(로그로 노출).

**자기위반 정직 보고**: float HP비율 1건이 유일한 실질 자기위반(반영 완료). `Evaluate` 서명이 설계 문서 `Evaluate(전장, 적)`와 다른 건 자기위반이 아니라 DG-1에서 승인받은 레이어 강제 정정.

---

## ⑥ 확정 설계 결정 (근거)

- **DG-1**: `BehaviorCondition.Evaluate`는 primitive만 받는다. 근거: struct가 SO(Data)에 직렬화되려면 Data 레이어여야 하는데, `EnemyUnit`(Core)을 받으면 Data→Core 역참조로 단방향 위반. primitive만 받아 Data 완결 + 설계 원형(struct+팩토리+Evaluate) 보존. 대상정책은 Core 목록 참조가 불가피해 enum만 Data, 선택 로직은 Systems(`TargetSelectionPolicy`)로 자연 분리.
- **DG-3**: `behaviorPatternId`(문자열) → 구현 매핑을 레지스트리 주입(`Dictionary<string, BehaviorPatternData>`)으로. 근거: A의 데이터 모델(문자열 키, "가리킬 SO 없음")을 존중, `EnemyUnitData` 스키마 무수정. 잡몹/보스 분기는 `EnemyUnit.IsBoss`로.
- **DG-5**: behavior 바인딩은 lazy(Decide 시 캐시 미스면 팩토리). 근거: `BattleFlowSystem`에 전투 시작 진입점 없음 + 웨이브 전환 시 새 적 자동 대응 + 같은 적 = 같은 인스턴스라 보스 `phaseIndex` 보존. 캐시(상태) 보유로 sealed instance class 정당.
- **DG-2**: 전장 입력을 `BattleSnapshot` struct 하나로(turnNum + 생존 아군·적 목록). 목록은 "생존 필터 완료본"이 계약(생성 측 책임), 소비 측 재필터 안 함. Systems 배치(호출자가 Systems뿐).
- **DG-4**: `skillId` → SkillData는 `enemy.Skills` 선형 탐색(스킬 수 적음, stateless). 미보유 시 로그 + 규칙 스킵.
- **DG-6**: 조건 5종·대상정책 4종 enum + 로직 전부 구현. 실제 채택(어느 잡몹이 뭘 쓰나, threshold 값)은 SO 데이터 자리(GPT 잡몹 스펙 gated).
- **DG-7**: 보스는 `BossBehaviorBase` abstract 골격까지(계약 + 안P 상태 + 저장 훅 자리). 실제 페이즈 로직은 보스 실물(v0.1.0 공개범위 밖) 등장 시 파생이 채움(YAGNI).
- **리뷰 반영 결정**: HP 비율 판정을 정수 교차곱으로. `_ratioScale=100`은 `CombatCalculator._scale`과 값은 같으나 목적이 다른 별개 로컬 상수(피해 배율 vs 비율 변환) — Data 레이어라 Systems 상수 참조 불가. "Scale 단일 소유" 규율은 피해 배율을 겨눈 것이라 위반 아님.

---

## ⑦ 기술 부채

### 해소
- **float HP비율 → 정수 교차곱** (리뷰 높음 반영). 결정론 크로스 플랫폼 안전성 확보.

### 미해소 (이월)
- **저장/복원 미배선**: `BossBehaviorBase.CaptureState/RestoreState` 훅 자리만 존재. `_phaseIndex/_sequenceStep`은 전투 중 저장→재개 시 복원돼야 시퀀스 재현(결정론 조건). → **묶음4 저장 스코프 편입 필수**(적AI 설계 §7, F 인계 부채 연장).
- **`livingAllyCount` 자신 포함 미확정**: 현재 자신 포함. "나 말고 N마리" 요구 시 `DataDrivenBehavior`에서 `-1` 한 줄. GPT 잡몹 스펙 확정 시 정리.
- **`Debug` 전역 로거 의존**: `DataDrivenBehavior`·`EnemyBehaviorSystem`이 `UnityEngine.Debug` 직접 사용(F의 M3과 동일 성격). 헤드리스 전환 시 로거 추상화와 함께 정리.
- **스냅샷 매 턴 할당**: `Step3`에서 `Where().ToList()` 2개. 필요 시 재사용 버퍼. 현재 과설계라 보류.

---

## ⑧ 후속 대주제 훅 위치

- **보스 실물 구현** → `EnemyBehaviorSystem.CreateBehavior`의 `if (enemy.IsBoss)` 블록. 여기에 `patternId → 파생 보스 클래스(Chapter17System·Chapter20System 등)` 매핑 추가. 현재는 무등록(무행동) + LogWarning.
- **보스 페이즈 전환** → `BossBehaviorBase.ShouldAdvancePhase`(판단만) + `_phaseIndex` 증가. 게이지·자세 리셋 실행은 묶음3 리셋 별 경로 재사용(behavior가 침범 금지 = D10).
- **저장 시스템** → `BossBehaviorBase.CaptureState/RestoreState`로 `phaseIndex·sequenceStep` 왕복. 묶음4.
- **잡몹 실데이터** → `BehaviorPatternData` SO 인스턴스 작성(조건·skillId·대상정책 채움). GPT 잡몹 스펙 수령 후.
- **레지스트리 조립** → 셋업/조립부가 `patternId → BehaviorPatternData` 딕셔너리 구성 후 `EnemyBehaviorSystem`에 주입(현재 프로브만 조립).

---

## ⑨ 미해결·TODO / 다음 착수 지점

### 세션 시작 전 수동 작업 (필수)
1. **코드 → GitHub `dev` 커밋**: 신규 11파일 + `BattleFlowSystem.cs` 수정. `_Scratch/G_IntegrationProbe.cs`는 커밋 제외. (최소 커밋 단위 = 소주제이나 G는 한 세션 완주라 대주제 단위 커밋 가능)
2. **본 인계문서 → 프로젝트 Knowledge 업로드**: 다음 세션이 읽을 사본.

### 실전투 동작 전제 (미완)
- **레지스트리 조립부 부재**: 현재 `patternId → BehaviorPatternData` 매핑을 구성·주입하는 셋업 코드가 없다. 프로브만 리플렉션으로 조립 중. 실제 전투를 굴리려면 조립부(전투 시작 시 등장 적들의 patternId를 모아 레지스트리 구성)가 필요 — EncounterData 통합(F 이월)과 함께 다룰 사안.
- **GPT 잡몹 스펙 대기**: 조건 kind·대상정책 실채택 목록, `SelfHpBelow` threshold 값, 잡몹별 규칙 우선순위. 구조는 완성, 값은 gated.

### 다음 대주제
- 공정표상 G 이후 대주제 확인 후 착수. 로드맵상 남은 항목: EncounterData 통합(F 이월), 페이즈 전환 실구현(보스 실물 등장 시), 저장/로드(묶음4).
