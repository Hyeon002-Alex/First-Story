# 인계문서 — K 대주제: 조회·선택지 Read Layer

**상태: 완료** (대주제 리뷰·반영까지 끝난 정규 종료)
**SSOT: `Hyeon002-Alex/First-Story` @ `dev`**

---

## 0. 요약

K = 전투 파이프의 **read/query 계층**. write/execute 측은 A~J로 완성돼 있었고 읽기 계층이 부재했음 — K가 그 공백을 채움. 헤드리스 프로브 방법론으로 다룰 수 있는 마지막 대주제(이후 씬 렌더링은 이 패러다임을 깸).

소주제:
- **K-1** IntentView — reveal 게이트된 적 intent 투영. Step4_Reveal 배선.
- **K-2** ChoiceQuerySystem — 아군 위상별 선택지 pull 산출.
- **K-3** 예상=실제 불변식 — 코드 산출 없음(최소안). 프로브로 못박음 + seam.

---

## 1. 산출물

**신규**
- `Assets/Scripts/Systems/Flow/IntentView.cs`
- `Assets/Scripts/Systems/Input/ActionChoice.cs`
- `Assets/Scripts/Systems/Input/AllyChoices.cs`
- `Assets/Scripts/Systems/Input/ChoiceQuerySystem.cs`
- `Tests/Probes/KProbe.cs` (커밋 대상 — I/J와 동일 프로브 승격 경로)

**수정**
- `Systems/Flow/IntentSystem.cs` — `+GetView`, `-AllIntents`(고아 제거)
- `Systems/Flow/BattleFlowSystem.cs` — `Step4_Reveal` 재작성(GetView 소비)
- `Systems/Flow/InfoResponseSystem.cs` — `+IsInfoActionSkill`(단일 소유), `IsInfoResponse` 재사용
- `Systems/Flow/ResponsePhaseSystem.cs` — `+DefenseApCost`/`+ProtectionApCost` 읽기 노출
- `Core/AllyUnit.cs` — `+UniqueAction` 노출

---

## 2. 핵심 구조

**IntentView** (Systems/Flow, 생산자 IntentSystem과 co-locate)
- reveal 게이트 투영. `Basic`(대상·방향) / `Full`(+행동명·부가효과·회피불가) 두 팩토리.
- 게이트를 **생성 단계에서 강제**: 미확인 view엔 확인정보가 물리적으로 없음(기본값). 상위 UI 버그로도 미공개 정보 유출 불가 → 정보전 은닉 규칙을 데이터 계층에서 보장.

**ChoiceQuerySystem** (Systems/Input, static)
- pull 산출: `GetChoices(ally, phase, snapshot)` → `AllyChoices`(ActionChoice 목록).
- **제안(offer) vs 집행(enforce) 분리**: 여긴 "무엇을 고를 수 있나"만. AP 판정 = `APSystem.CanAfford`, 대응 AP = `ResponsePhaseSystem` 단일 소유를 읽음(복제 0).
- 정보형 자격 = `InfoResponseSystem.IsInfoActionSkill` 단일 소유 호출(제안·집행 일원화).

**ActionChoice** (Systems/Input)
- 종류별 팩토리 4종: `Defense(dir)` / `Protection(targets)` / `InfoAction(skill, targets)` / `EndTurn()`.
- 대상 필수 오퍼는 후보 0이면 **미생성**(고를 수 없는 오퍼는 오퍼가 아님). `RequireTargets` 가드.

**GetValidTargets(actor, rule, pool)** (public, ChoiceQuerySystem)
- 규칙 해소 SSOT. Self→[actor] / Single·Area·FixedTarget→pool 복제.
- **진영 지식은 함수 밖**: target-side가 데이터에 없어 진영은 호출 맥락이 pool로 주입(보호=아군, 정보확인=적).

---

## 3. 공개 인터페이스

- `IntentSystem.GetView(EnemyUnit)` → `IntentView | null` (미등록 null)
- `IntentView.Basic/Full` (팩토리) · 프로퍼티 `Target/Direction/IsRevealed/DisplayName/Effects/IsUnavoidable`
- `ChoiceQuerySystem.GetChoices(AllyUnit, InputPhase, BattleSnapshot)` → `AllyChoices`
- `ChoiceQuerySystem.GetValidTargets(BattleUnit, TargetRule, IReadOnlyList<BattleUnit>)` → `IReadOnlyList<BattleUnit>`
- `AllyChoices.Ally/Phase/Choices`
- `ActionChoice.Kind/Skill/Direction/ApCost/ValidTargets` + 팩토리 4종
- `InfoResponseSystem.IsInfoActionSkill(SkillData)` → bool (신규 단일 소유)
- `ResponsePhaseSystem.DefenseApCost/ProtectionApCost` (읽기)
- `AllyUnit.UniqueAction`

---

## 4. 의존성 (계층 방향 Systems→Core→Data 준수, 역참조 없음)

- `IntentView` → Data(`AttackDirection`,`StatusEffectData`), Core(`BattleUnit`). Systems 배치.
- `ChoiceQuerySystem`/`ActionChoice`/`AllyChoices` → Systems(`APSystem`,`ResponsePhaseSystem`,`InfoResponseSystem`,`BattleSnapshot`), Core(`AllyUnit`/`EnemyUnit`/`BattleUnit`), Data(`SkillData`,열거).
- 결과 타입을 Systems에 둔 이유: K-3 preview(`DamageResult`, Systems) 부착 여지 확보 + 생산자 co-locate. Core에 뒀으면 DamageResult 역참조로 봉쇄됨.

---

## 5. 검증 + 리뷰 결과·반영

**통합 프로브**: `KProbe` 46 PASS / 0 FAIL. I 38 · J 31 회귀 유지. `bash Tests/run.sh` 전체 PASS.

**리뷰**: 높음 없음. 중간 3건 **전부 반영**:
- (a) `[K-3 예정]` seam 주석 부정확 → `[Action seam 수렴]`으로 정정(K-3 최소마감으로 예상피해 미부착).
- (b) `AllIntents` 고아 public API(소비처 0) → 제거. GetView + 슬롯순 순회가 완전 대체.
- (c) 정보형 자격 판정 이원화 → `InfoResponseSystem.IsInfoActionSkill` 단일 소유 신설, 제안·집행 일원화.

낮음 3건 기록:
- (d) read-layer 결과 타입 폴더 분산(IntentView=Flow, 선택지 타입=Input). 각자 생산자 co-locate라 규칙 준수, 조직적 사안.
- (e) 대응 AP **값** 하드코딩(데이터 자산 이관)은 이월. K는 복제만 제거.
- (f) Step4 슬롯순 순회는 프로브에서 예외 없음(스모크)만 검증 — muted 로그라 순서 단언 불가. K-2 결정론은 `SequenceEqual`로 단언됨.

---

## 6. 확정 설계 결정 (근거)

- **Pull 채택(vs Push).** `ChoiceQuerySystem` 요청 시 산출, `InputRequest` 무변. 근거 3: ①유효대상은 스킬의 함수라 flat 부착 불가 ②`InputRequest`(Core)는 `DamageResult`(Systems) 못 실어 K-3 봉쇄 ③소비처(UI) 없는 Core 필드 선노출 = M1 위반.
- **결과 타입 Systems 배치.** preview(DamageResult) 부착 여지 + 생산자 co-locate.
- **2안: 진영 자명 행동만 완전 산출.** 정보확인·방향방어·보호·차례종료 완전, Action 위상 스킬 대상은 seam. 근거: `TargetRule`에 진영 없음 + 캐릭터 스킬 GPT 블록 → 진영 추론은 투기적(M1).
- **K-3 최소안.** `PreviewDamage`는 이미 `ComputeDamage` 재사용 → 예상=실제 구조 보장. 배선 대상(공격 오퍼 / 공개 피해) 둘 다 블록 → 코드 산출 없이 불변식만 프로브로 못박음 + seam.

---

## 7. 기술 부채

**해소**
- **J-L3** (Step4_Reveal의 IsRevealed 미소비) → GetView 소비로 해소.
- 대응 AP **복제 위험** → ResponsePhaseSystem 읽기 노출로 제거.

**미해소·이월**
- **I-D2** (InputRequest 선택 필드) → 실제 소비처인 **UI 대주제로 정직 이월**(pull 채택으로 InputRequest 무변).
- 대응 AP **값** 하드코딩(방향방어 1·보호 1 상수) → 데이터 자산 이관 이월.
- 방어대응 가능 스킬(도발) `SkillData` 자격 플래그 → 미정.
- Global `Debug` 로거 의존 → 이월.

---

## 8. 후속 대주제 훅 위치

- **Action 위상 스킬 선택지**: `ChoiceQuerySystem.ActionChoices()` seam. 수렴 절차 주석에 명시(진영 pool 결정 → `GetValidTargets` → `APSystem.CanAfford` 게이팅, 후보 0이면 오퍼 생략).
- **공격 오퍼 예상피해**: `ActionChoice`에 Preview 필드 추가 지점(Action seam과 동시). `ActionResolver.PreviewDamage` 재사용.
- **방어위상 받는피해 preview(정보전 핵심)**: 별도 대주제 후보. 가정-자세 preview(`ComputeDamage` 자세 오버라이드) + `ChoiceQuerySystem`↔`IntentSystem` 결합 필요.
- **IntentView 면역 필드**: `// [캐릭터 스킬 설계 후 수렴]` 지점.

---

## 9. 미해결·다음 착수 지점

**GPT(What) 요청 항목** — What/How 경계:
- **`SkillData`에 target-side(대상 진영) 필드 필요.** `TargetRule`은 대상 모양만 담음(Single/Area/FixedTarget/Self). 진영이 없으면 Action 위상 스킬 대상 산출 **영구 불가**. → GPT 단계 주입문서 반영 요청.
- **공개(reveal)/방어 위상이 예상피해를 노출하는가, 자세 가정은 무엇인가**(undefended? 방향별?) — GDD/What 결정.
- 캐릭터 스킬 설계(값·진영·정보형 여부) 상위 블록 — 여전히 GPT 대기.

**다음 착수 후보**
- 씬 UI 대주제 — read layer 존재로 이제 착수 가능. I-D2·Preview 필드가 여기서 소비됨.
- KProbe 승격 규율 적용(리플렉션 헬퍼 잔존 — 5단계 승격 기준 검토).
- `SkillData` target-side 도착 시 Action 위상 선택지 수렴.

**세션 시작 전 수동 작업**
1. K 코드 → GitHub `dev` 커밋 (신규 5 파일 + 수정 5 파일 + `Tests/Probes/KProbe.cs`).
2. 본 인계문서 → 프로젝트 Knowledge 업로드.
3. (위생·K 무관) `Tests/gitignore` 파일명 오타 — 앞점 없어 `_bin/`이 실제로 무시되지 않음. `.gitignore`로 정정 권고.
