# 인계문서 — 4단계 L대주제 — Action 위상 완성

**상태**: 완료 - 사후 수정 반영

**근거 문서**: `K_후속_GPT_추가명세_v1.0`, `L_사후_수정_지시서_v1.0`

---

## 0. 사후 수정 경위

최초 `완료` 선언 이후, 상위 GPT 명세(`K_후속_GPT_추가명세_v1.0`)와 사후 감사 결과 4건의 P0 결함이 확인되어 `조건부 완료` 상태로 재분류, 전부 수정·검증 완료 후 이 문서로 최종 갱신한다.

| 항목 | 문제 | 처리 |
|---|---|---|
| Step7 AP 미소모 | Action 위상 실제 실행 경로가 AP를 안 깎음 | 수정(사후-1) |
| 도전 부당 제외 | GDD 구형 서술과 혼동해 정상 Action 스킬을 제외 처리함 | 재분류·구현(사후-3) |
| 탐세 실행 순서 | AP/acted가 피해보다 먼저 커밋(실패 안전성 위반) | 수정(사후-2) |
| KProbe 미커밋 | K의 "46 PASS" 주장이 회귀 검증 근거 없었음 | 복구(사후-4) |

---

## 1. 산출물

### 원 L 산출물
- `SkillData.TargetSide` 필드
- `ChoiceQuerySystem.ActionChoices()` 실제 산출 로직
- `ActionChoice.PreviewDamages` 무방어 예상피해
- `Tests/Probes/LProbe.cs` (24 PASS)

### 사후 수정 산출물
- **사후-1**: `BattleFlowSystem.Step7_ExecuteBySpeed` — Action 위상 AP 실소모(`ConsumesActionAp`)
- **사후-2**: `InfoResponseSystem.TryValidate`(신규, `TryApply` 대체) — 순수 검증으로 축소. `Step5_InfoResponse`가 reveal→피해/붕괴→AP→acted 순서를 직접 오케스트레이션
- **사후-3**: `SkillData.IsChallenge` 필드, `ChallengeSystem.cs`(신규) — 도전의 "다음 글로벌 턴 대상강제" 예약·적용. `BattleFlowSystem` 생성자에 `ChallengeSystem` 추가, `Step3_AssignEnemyIntent`/`Step7_ExecuteBySpeed`에 훅 배선
- **사후-4**: `Tests/.gitignore`(파일명 수정, 기존 `Tests/gitignore` 대체), `Tests/Probes/KProbe.cs`(신규, 31 PASS)

### 터치한 파일 전체
| 파일 | 변경 |
|---|---|
| `Assets/Scripts/Data/Enums/TargetSide.cs` | 신규(L) |
| `Assets/Scripts/Data/Skills/SkillData.cs` | `TargetSide`(L) + `IsChallenge`(사후-3) 필드/프로퍼티 |
| `Assets/Scripts/Systems/Input/ActionChoice.cs` | 전문(L, `PreviewDamages` 도입) |
| `Assets/Scripts/Systems/Input/ChoiceQuerySystem.cs` | `ActionChoices` 등 신설(L) + stale 주석 2건 정정(사후-3 리뷰) |
| `Assets/Scripts/Systems/Execution/ActionResolver.cs` | `ComputeDamage`/`PreviewDamage` static화, `HasDamage` public화(L) |
| `Assets/Scripts/Systems/Flow/InfoResponseSystem.cs` | 전문 교체 — `TryApply`→`TryValidate`(사후-2) |
| `Assets/Scripts/Systems/Flow/BattleFlowSystem.cs` | `Step5_InfoResponse` 오케스트레이션(사후-2), `Step7_ExecuteBySpeed` AP소모+도전등록(사후-1·3, 리뷰로 순서 재조정), `Step3_AssignEnemyIntent` 도전적용 훅(사후-3), 생성자에 `ChallengeSystem` 추가(사후-3) |
| `Assets/Scripts/Systems/Flow/ChallengeSystem.cs` | 신규(사후-3) |
| `Assets/Scripts/Systems/Assembly/BattleBootstrapper.cs` | `ChallengeSystem` 배선(사후-3) |
| `Tests/.gitignore` | 파일명 수정, `Tests/gitignore` 대체(사후-4) |
| `Tests/Probes/IProbe.cs` | `Build()` 헬퍼에 `ChallengeSystem` 주입(사후-3, 생성자 시그니처 변경 대응) |
| `Tests/Probes/JProbe.cs` | `_executor` 주입(L-3), J-2 섹션 `TryValidate`로 전환(사후-2) |
| `Tests/Probes/KProbe.cs` | 신규(사후-4) |
| `Tests/Probes/LProbe.cs` | 신규(L) |

---

## 2. 핵심 구조

### Action 위상 (L, 변경 없음)
`ActionChoices()`가 `ally.UniqueAction`(비정보형만) + `ally.EquippedSkills`(3개)를 순회, `skill.TargetSide`로 `SidePool`이 대상 진영을 정하고 `GetValidTargets`가 `TargetRule` 규칙을 적용, `APSystem.CanAfford`로 제안을 게이팅. 도전은 Hostile/Single 일반 스킬이라 이 일반 경로에 특수 취급 없이 자연 편입된다(사후-3).

### Action 위상 실제 집행 (사후-1)
`Step7_ExecuteBySpeed`가 `ConsumesActionAp(command.Kind)`로 비정보형 고유행동/장착스킬을 가려 AP를 게이팅·소모한다. 소모는 **`_executor.Execute` 이후**(실패 안전성 — Execute가 예외로 중단되면 AP는 저절로 미소모 상태로 남음, `Step5_InfoResponse`와 동일 원칙).

### 정보형 고유행동 (사후-2)
`InfoResponseSystem.TryValidate`는 순수 검증(대상 유효성 + AP 지불가능성)만 하고 `ally`/`enemy`/`apCost`를 `out`으로 반환한다. 상태 변경은 전부 `Step5_InfoResponse`가 소유: `SetRevealed` → `_executor.Execute`(피해+붕괴) → `APSystem.Consume` → `SetActed`. 순차 호출이므로 중간에 예외가 나면 뒤 단계가 저절로 실행되지 않는다.

### 도전 (사후-3)
`SkillData.IsChallenge` 플래그로 자격을 표시한다. Step7에서 도전 사용을 감지하면 `ChallengeSystem.Register(caster, target, currentTurn)`을 호출해 "다음 글로벌 턴" 예약을 건다. 다음 턴 `Step3_AssignEnemyIntent`가 적 intent를 결정한 직후(`Step4_Reveal` 공개 전) `ChallengeSystem.ApplyReservations`가 해당 적의 intent를 검사한다 — `TargetRule.Single`(유도 가능한 단일 직접 공격)이면 `IntentSystem.SetIntent`로 대상을 캐스터로 덮어쓰고 예약을 소거, 아니면(Area 등) 그 턴 기회를 소진하고 예약을 그냥 소거한다(무기한 유지 안 됨). **보호(Protection)와 다른 메커니즘**임에 주의 — 보호는 실행 시점(Step7, TargetingSystem)에 리다이렉트하지만, 도전은 공개(Step4) 전에 intent 자체를 바꿔 UI가 처음부터 강제 대상을 보여준다.

---

## 3. 공개 인터페이스

```csharp
// Data
public bool SkillData.IsChallenge { get; }   // 사후-3

// Systems/Flow — InfoResponseSystem (시그니처 변경, 사후-2)
public static bool InfoResponseSystem.TryValidate(
    ActionCommand command, out AllyUnit ally, out EnemyUnit enemy, out int apCost);
// TryApply는 더 이상 없음. 상태 변경 없는 순수 검증으로 대체됨

// Systems/Flow — ChallengeSystem (신규, 사후-3)
public static bool ChallengeSystem.IsChallengeSkill(SkillData skill);
public void ChallengeSystem.Register(AllyUnit caster, EnemyUnit target, int usedTurn);
public void ChallengeSystem.ApplyReservations(IntentSystem intentSystem, int currentTurn);

// Systems/Flow — BattleFlowSystem (생성자 시그니처 변경, 사후-3)
public BattleFlowSystem(
    IReadOnlyList<AllyUnit> allies, List<EnemyUnit> enemies,
    IntentSystem intentSystem, ProtectionSystem protection, ChallengeSystem challenge,
    IActionExecutor executor, WaveSystem waveSystem, EnemyBehaviorSystem behaviorSystem);
// challenge 파라미터가 protection 다음에 추가됨(8-param)
```

L 원본의 `TargetSide`/`ActionChoice.PreviewDamages`/`ActionResolver.PreviewDamage`(static)/`HasDamage`(public static)는 변경 없음.

---

## 4. 의존성

- `ChallengeSystem`(Systems/Flow)은 `IntentSystem`/`EnemyIntent`/`TargetRule`(Data)만 참조. `Laplace.Systems → Core → Data` 단방향 유지, asmdef 위반 없음.
- `BattleFlowSystem`이 `ChallengeSystem` 인스턴스를 소유(생성자 주입), `BattleBootstrapper`가 실제 조립에서 배선.
- `InfoResponseSystem`은 이제 `IntentSystem`을 전혀 참조하지 않음(사후-2로 `TryValidate`가 순수 검증만 하면서 제거됨) — 시그니처가 가벼워짐.

---

## 5. 검증 결과 + 리뷰 결과·반영

**통합 프로브**: `IProbe` 38 / `JProbe` 33 / `KProbe` 31 / `LProbe` 24 = **126 PASS / 0 FAIL**. `bash Tests/run.sh` 전체 통과.

**코드 리뷰(사후 수정 대상)**:

| 심각도 | 내용 | 반영 |
|---|---|---|
| 높음 | Step7의 AP 소모가 `Execute` 이전에 일어나 사후-2가 막으려던 것과 동일한 실패 위험(실행 실패해도 AP는 나감)을 사후-1이 새로 심음. 작성 당시 주석("실행 실패해도 AP 없이 결과 없는 상태 안 만듦")도 실제로는 반대 결과를 서술하는 오류였음 | 반영 — 소모를 `Execute` 이후로 이동, `Step5_InfoResponse`와 순서 통일 |
| 중간 | `ChoiceQuerySystem.ActionChoices`/`DefenseChoices`의 stale 주석 2건(도전이 아직 제외 대상인 것처럼 서술) | 반영 — 전열 수호만 남았다고 정정 |

원 L 리뷰(높음 2 / 중간 1)는 이전 갱신에서 이미 반영 완료.

---

## 6. 확정 설계 결정(근거)

### 원 L 결정 (변경 없음)
`SidePool`은 `actor` 미수신(아군 전용 고정 매핑), `PreviewDamages`는 `HasDamage` 게이팅, `ComputeDamage`/`PreviewDamage`/`HasDamage` static 전환.

### 사후 수정 결정
- **Step5/Step7 AP·상태 커밋 순서를 "실행 이후"로 통일.** 실패 안전성(실행 실패 시 부작용 미확정)을 두 경로 모두에서 보장. try/catch 없이 순차 호출만으로 달성 — 뒤 단계는 앞 단계가 성공해야 도달하므로 자동으로 안전.
- **도전 등록은 `ActionResolver` 안이 아니라 `BattleFlowSystem.Step7`에서 직접.** 도전은 피해 0이라 `ActionResolver.Execute`를 그대로 태우면 no-op. 계산 파이프(ActionResolver)와 무관한 신규 부수효과라 `ActionResolver`에 넣으면 책임이 섞임 — `InfoResponseSystem`을 Step5가 오케스트레이션하는 것과 같은 패턴으로 Step7이 직접 소유.
- **도전의 대상 강제는 실행 시점(Step7)이 아니라 intent 결정 직후(Step3, Step4 이전)에.** 보호(Protection)는 실행 시점 리다이렉트라 실제 실행 전까지 원래 대상이 보이지만, 도전은 "Step4 공개 전에 강제 대상이 최종 intent에 반영되어야 한다"는 상위 명세 요구로 메커니즘 자체가 다름. 이 차이를 코드 배치(Step3 vs Step7)로 그대로 드러냄.
- **"유도 가능한 단일 직접 공격" 판별은 새로 안 만듦.** `TargetingSystem`의 보호 리다이렉트가 이미 쓰는 `TargetRule.Single` 기준을 그대로 재사용(SSOT). "시스템상 강제 불가 표시" 제외 카테고리는 현재 그런 플래그가 없어 공집합 — 필요해지면 필드 추가할 seam으로만 남김(M1).
- **KProbe는 K의 원 계약 전부 + L 이후 추가 계약을 함께 검증.** `LProbe`와 일부 겹치나, 사후 수정 지시서가 명시적으로 요구했고 K 자체 계약(K는 원래 회귀망 밖이었음)을 별도로 못박는 게 목적이라 중복을 감수함.

---

## 7. 기술 부채

**해소(사후 수정)**:
- Step7 AP 미소모
- 도전 부당 제외
- 탐세 실행 순서(실패 안전성)
- KProbe 미커밋
- `Tests/gitignore` 파일명 오타

**미해소(이월)**:
- **전열 수호 방어대응가능 자격 플래그.** 여전히 미정. `ResponsePhaseSystem.IsResponseKind`(20행)에 확장 seam 존재. SkillData 자산 미생성으로 대응 중.
- **persistent Full-intent(인카운터 상시 Full 공개 정책).** `K_후속_GPT_추가명세` §2.2로 확정된 정책이나 이번 배치에서 구현 안 함. **M(방어위상 정보전 예상피해) 착수 전 필수 선행 작업**으로 명시한다 — M의 방어 preview가 Full reveal 여부에 의존하므로, M 설계 전에 반드시 먼저 처리.
- **PreviewDamages 조회-확정 시차.** 여전히 실증 안 됨(`ChoiceQuerySystem.GetChoices` 호출부 없음). UI 대주제에서 "확정 직전 재조회" 원칙 필요.

---

## 8. 후속 대주제 훅 위치

- **M(방어위상 정보전 예상피해)**: `ComputeDamage` 자세 오버라이드 신규 필요. `IntentSystem`↔`ChoiceQuerySystem` 결합 필요. **선행 조건: persistent Full-intent 정책 구현.**
- **UI 대주제**: `ChoiceQuerySystem.GetChoices` 호출부 아직 없음. 첫 배선 지점.
- **방어대응가능 플래그 대주제(전열 수호)**: `ResponsePhaseSystem.IsResponseKind` 20행.
- **적 AI가 Friendly/Hostile SkillData를 실제 사용하는 대주제**: `TargetSide`를 대상 후보 진영 결정의 SSOT로 소비해야 함. 아군 전용 `ChoiceQuerySystem`의 고정 매핑(`SidePool`)을 적 AI에 복제하지 않을 것 — `TargetSide`의 상위 의미는 행동자 기준 상대 진영이라 적이 캐스터일 때도 같은 규칙이 적용돼야 하나, 현재 `SidePool`은 아군 전용으로 고정돼 있어 그대로 재사용 불가.

---

## 9. 미해결·TODO / 다음 착수 지점

**세션 시작 전 수동 작업**:
1. 코드 → GitHub `dev` 커밋
2. 이 인계문서(갱신본) → 프로젝트 Knowledge 업로드(기존 L 인계문서 대체)
3. 칼리프 SkillData 자산 입력 — 이제 도전 포함 6개(방패 강타·도전·불퇴·응징·견제·밀어붙이기) 입력 가능. 전열 수호만 계속 보류.

**다음 대주제**: M(방어위상 정보전 예상피해). 착수 전 persistent Full-intent 정책 구현이 선행 조건(§7).
