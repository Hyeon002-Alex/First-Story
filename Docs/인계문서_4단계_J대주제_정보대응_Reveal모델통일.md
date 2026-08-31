# 인계문서 · 4단계 · J 대주제(정보 대응 + Reveal 모델 통일) · **완료**

- **상태: 완료** (J-1~J-3 구현 + 마감 4순차 종료: 통합 프로브 → 코드 리뷰 → 리뷰 반영 → 본 문서)
- 대상 브랜치: `dev` (SSOT)
- **세션 1개 완주** — 소주제 3개라 분할선 없음. 진행중 스냅샷 없음.

---

## 0. 대주제 목표(달성)

I(아군 입력)가 방어 대응(Step6)을 완성했듯, 정보 대응(Step5)을 대칭 완성하고 `ActionKind.Reveal` 모델을 GDD 정의로 통일했다. **배관 완성**이 목표 — 정보 공개의 화면 표시(조회 API)와 캐릭터별 공개 범위는 소비처(UI·캐릭터 스킬 설계) 미확정이라 의도적으로 이연했다(I의 D2 논리 재적용).

**핵심 발견**: "Reveal 대주제"는 데이터 은닉 시스템을 새로 만드는 게 아니다. 모든 정보는 이미 `EnemyIntent.Skill`에 있고, `IntentSystem._revealed`(HashSet)는 데이터를 숨기는 게 아니라 **공개 수준 게이트**다. 은닉 인프라는 완성돼 있었고, 남은 알맹이는 ①모델 통일 ②Step5 배선 ③정보형 고유행동 자격·실행 셋뿐이었다.

**소주제 3개 완료**: J-1 모델 통일 / J-2 InfoResponseSystem / J-3 Step5 배선 + Step6 acted 게이트.

---

## 1. 산출물

| 파일 | 계층 | 변경 |
|---|---|---|
| `Data/Enums/ActionKind.cs` | Data | **`Reveal` 제거**(전문 교체). 주석에 통일 모델 명시 |
| `Core/ActionCommand.cs` | Core | **`CreateReveal` 팩토리 제거**. 정보형은 `CreateUnique` 재사용 |
| `Data/Skills/SkillData.cs` | Data | `_isInfoAction` 필드 + `IsInfoAction` 프로퍼티 추가 |
| `Systems/Flow/InfoResponseSystem.cs` | Systems | **신규**. 정보형 고유행동 자격판정·적용(static) |
| `Systems/Flow/BattleFlowSystem.cs` | Systems | Step5(void→IEnumerator)·ExecuteTurn(Step5 재-yield)·Step6(acted 게이트) 수정 |

**회수(J 밖 · I 산출물)**:
| 파일 | 변경 |
|---|---|
| `Systems/Flow/BattleFlowSystem.cs` | `BuildOrder` tuple 요소명 명시화 `(unit: unit, slotIndex: slotIndex)`. I §7 개선안 미반영분. **J 커밋과 분리한 별도 `fix` 커밋** |

---

## 2. 핵심 구조

- **정보 공개 모델 = 게이트, 은닉 아님**: `EnemyIntent.Skill`이 방향·행동명·부가효과·회피불가의 진실원. `IntentSystem._revealed`는 "정보확인 후 공개" 수준으로 올라갔는지의 플래그. 정보확인 = `SetRevealed(enemy)`로 게이트 ON. 데이터는 안 바뀐다. 매 턴 `IntentSystem.ClearAll`이 `_revealed`를 리셋 → 정보확인은 그 턴 한정(GDD 정합).
- **정보형 고유행동 = `ActionKind.UniqueAction` + `SkillData.IsInfoAction`** [J 확정 모델]. 별도 종류가 아니다. GDD §278("연: 모든 고유 행동 변형은 정보확인 기능을 유지") = 정보확인은 고유행동 내장 기능.
- **`InfoResponseSystem`(신규, static)**: 방향방어·보호(`ResponsePhaseSystem`)와 대칭. 그 시스템 주석("정보확인은 이 시스템 밖")이 규정한 경계를 받는다. 정보형은 스킬 기반이라 분리(한 클래스 한 파일).
- **Step5 배선(J-3)**: Step6 패턴 대칭. 생존 아군마다 `InputPhase.Info` 요청 → 응답이 `IsInfoResponse`면 `TryApply`. EndTurn·부적격은 무시(정보 대응 포기). `ExecuteTurn`이 `while(s.MoveNext()) yield return s.Current;`로 재-yield.
- **acted 게이트(J-3)**: 정보 대응 성공 시 `SetActed(true)`. Step6는 `|| a.ActedThisTurn`로 스킵, Step7은 I의 `IsStillValid` 기존 D5 게이트로 자동 스킵. GDD §409("대응한 유닛은 속도순 실행 단계에서 다시 행동하지 않는다") 일치.

---

## 3. 공개 인터페이스

```csharp
// SkillData — J-1
public bool IsInfoAction => _isInfoAction;   // 정보형 고유행동 자격

// InfoResponseSystem — J-2 (신규 static)
public static bool IsInfoResponse(ActionCommand command);
    // UniqueAction + Skill.IsInfoAction만 true. null·기타 종류 false. "정보행동 집합" SSOT
    // ResponsePhaseSystem.IsResponseKind가 ActionKind만 보는 것과 달리 스킬 표식까지 봐야 해 ActionCommand를 받음
public static bool TryApply(ActionCommand command, IntentSystem intentSystem);
    // 적용 true(대상 적 SetRevealed·AP(스킬 apCost) 소모·acted)
    // AP부족·대상무효(전투불능 적/비적) false / 스코프 밖 명령·null·비아군 주체 throw

// BattleFlowSystem — J-3
private IEnumerator Step5_InfoResponse();          // void→IEnumerator. InputPhase.Info 요청 왕복
// ExecuteTurn: Step5 재-yield 블록 추가(Step6·7 대칭)
// Step6_DefenseResponse: continue 조건에 `|| a.ActedThisTurn` 추가

// 제거됨
// ActionKind.Reveal / ActionCommand.CreateReveal
```

---

## 4. 의존성

- **InfoResponseSystem**: `ActionCommand`·`AllyUnit`·`EnemyUnit`(Core), `SkillData`(Data), `IntentSystem`·`APSystem`(Systems 동계층). 전부 정방향/동계층 — `Systems → Core → Data` 단방향 준수.
- **J-3**: 기존 `InputRequest`·`ActionCommand`·`AllyUnit` + `InfoResponseSystem`(J-2) + `IntentSystem`. 커밋 반경 = `BattleFlowSystem` 단일.
- `BattleFlowSystem` 생성자·`BattleBootstrapper.Build` **불변**(입력 소스 미주입, I의 D3-B 유지).

---

## 5. 검증 결과 + 리뷰 결과·반영

### 통합 프로브 (마감 4순차 1단계)
- 방식: `_Scratch/J_IntegrationProbe.cs`(커밋 제외) + `_Scratch/UnityEngineStub.cs`. `FormatterServices.GetUninitializedObject`로 BattleFlowSystem 조립 후 `_allies`·`_intentSystem`·`_protection` 3필드만 리플렉션 주입 → executor 등 무거운 의존 없이 Step5·6 직접 펌프. J-2는 InfoResponseSystem 직접 호출.
- **결과: PASS 27 / FAIL 0**.
- 컴파일: mono-mcs 6.8, `-langversion:latest`. `Debug` 음소거 스위치로 전투 로그 억제.

검증 항목 요약:
- **J-1**(컴파일 자체로 검증): `ActionKind.Reveal` 부재 / `CreateReveal` 부재 / `SkillData.IsInfoAction` 존재 상태로 전체 소스 컴파일 성공.
- **J-2**(18): `IsInfoResponse` 4종(정보형 true·일반 고유행동 false·방향방어 false·null false) / `TryApply` 정상(반환 true·대상 `IsRevealed`·AP 2 감소·acted) / AP부족(false·미공개·AP무변·acted 안 켜짐) / 대상무효 2종(전투불능 적·아군) / throw 4종(비정보 명령·null command·null intentSystem·비아군 주체).
- **J-3**(9): Step5 생존 아군 A·B에 `InputPhase.Info` 발행 + 전투불능 Dead 제외 + Phase 정합 / A 정보대응→대상 `IsRevealed`·acted / B 포기→acted false / Step6에서 acted된 A 요청 없음·미행동 B 요청 있음·Phase 정합.

### 코드 리뷰 (마감 4순차 2단계)
- **높음: 없음** — J 자기결정 5건(모델 통일·시스템 분리·공격효과 seam·acted 게이트·AP 하드코딩 회피) 위반 없음. 계층 단방향·SSOT·seam 소유 표시 준수.
- **중간: 없음** — I의 검증된 Step6 패턴을 대칭 복제, 설계를 코드 착수 전 확정해 자기위반 여지가 적었음.
- **낮음**(수용·기록):
  - **L1** — 정보 공개 단일 대상 한정. `TryApply`가 `command.Target` 단일 적만 `SetRevealed`. "전체 공개" 확정 시 `SetRevealed` 반복 호출로 확장(구조 변경 없음).
  - **L2** — Step5의 `TryApply` 반환값 무시. 실패가 조용히 넘어감(I L3과 동일 정책). 실패 시 acted 안 켜져 Step6·7로 넘어가는 게 정합적이라 배관 단계 무해. 실패 피드백은 UI 몫.
  - **L3** — `SetRevealed` 소비처 현 단계 미활용. `Step4_Reveal`은 `IsRevealed`를 안 보고 기본 공개(대상·방향)만 로그. 정보확인 효과(행동명·부가효과)를 드러내는 조회 API가 아직 없음(프로브는 `IsRevealed` 직접 조회로 검증). 소비처는 UI로 이연.
  - **L4** — 판정 시스템 네이밍 비대칭(`ResponsePhaseSystem` vs `InfoResponseSystem`). `ResponsePhaseSystem`(I 산출물) 개명 비용이 커 기록만.

### 리뷰 반영 (마감 4순차 3단계)
- J 소스 변경 **없음**(높음·중간 없음, 낮음 4건 전부 인계 기록).
- **BuildOrder tuple 명시화 회수**(I §7): 프로브에서 mono-mcs name inference 컴파일 실패로 발견. J와 분리한 별도 `fix` 커밋. 5단계 Tests 승격의 전제(헤드리스 컴파일)를 막던 결함이라 회수 우선순위 높음.
- 반영 후 프로브 회귀 PASS 27/0 유지.

---

## 6. 확정 설계 결정(근거)

1. **옵션1 · Reveal 모델 통일** — `ActionKind.Reveal`(스킬 없는 고정 종류) 제거, 정보형 고유행동 = `UniqueAction` + `SkillData.IsInfoAction`. 근거: GDD §278("모든 고유 행동 변형은 정보확인 기능을 유지" = 고유행동 내장 기능), §627("정보 기능을 가진 고유 행동"), §409("정보형 고유 행동"). 코드에 `ActionCommand.Skill` 필드·`CreateUnique` 팩토리가 이미 있어 재사용. 반대 옵션2(고정행동)는 GDD 위반(보호는 전원 공통, 정보확인은 연·니아만), 옵션3(Reveal+스킬)은 UniqueAction과 중복.
2. **ResponsePhaseSystem 밖 별도 시스템** — 그 시스템은 "스킬 없는 고정 대응만" + 주석 "정보확인은 이 시스템 밖". 정보형은 스킬 기반이라 성격이 달라 대칭 분리(한 클래스 한 파일).
3. **공격 효과 seam 이연** — `TryApply`는 `SetRevealed`·소모까지만. 정보형 고유행동의 공격 겸용 여부는 GDD §627이 캐릭터 스킬 설계로 미룸(미제공). 공격 확정 시 seam 지점에 `ActionResolver` 경로 연결. 지금 넣으면 미확정 스펙 위 투기.
4. **acted 게이트** — 정보 대응 성공 시 `SetActed(true)`, Step6에 acted 게이트 추가. 근거: GDD §409 "이번 턴 행동 소모". Step7은 I의 D5 게이트로 이미 스킵.
5. **AP 하드코딩 회피** — `command.Skill.ApCost` 사용. `ResponsePhaseSystem`은 방향방어·보호가 스킬 없어 비용 하드코딩(I 이월 부채)했지만, 정보형은 스킬이 값을 소유해 그 부채를 반복하지 않음.
6. **IsInfoResponse가 ActionCommand 인자** — `IsResponseKind(ActionKind)`는 스킬 없는 고정행동이라 Kind만으로 충분하지만, 정보형은 `UniqueAction` 중 `IsInfoAction` 켜진 것만이라 `ActionCommand`를 받아야 구분(시그니처 비대칭의 근거).

---

## 7. 기술 부채

**해소**
- **BuildOrder tuple 명시화**(I §7 개선안 회수) — mono-mcs 헤드리스 컴파일 가능해짐. 5단계 Tests 승격 전제 확보.

**미해소 이월**
- **정보 공개 범위 세부**(D2 이연) — "무엇이 정보확인 후 공개되나"의 조회 API(기본 공개 vs 정보확인 후 공개 구분 반환). 소비처 = UI 대주제.
- **캐릭터별 공개 범위**(연/니아 차이) — GDD §627 "별도 캐릭터 스킬 설계에서 확정". GPT 미제공.
- **정보형 고유행동 공격 겸용 여부** — 캐릭터 스킬 설계 확정 후 `InfoResponseSystem` seam에 `ActionResolver` 연결. seam 주석 존재.
- **정보 공개 단일 대상 한정**(L1) — "전체 적 공개" 확정 시 `SetRevealed` 반복으로 확장.
- **ResponsePhaseSystem AP 하드코딩**(I 이월) — 방향방어 1·보호 1. J는 스킬 apCost로 회피했으나 그 시스템은 미해소. 밸런스 데이터 이관 대상.
- **`Debug` 전역 로거 의존**(H·I 이월) — 신규 `InfoResponseSystem`도 동일. 새 부채 아님.
- **판정 시스템 네이밍 비대칭**(L4) — `ResponsePhaseSystem` vs `InfoResponseSystem`.

---

## 8. 후속 대주제 훅 위치

- **UI 대주제**:
  - 정보 조회 API: `IntentSystem.IsRevealed`를 보고 기본 공개(대상·방향) vs 정보확인 후 공개(행동명·부가효과·특수대상·회피불가·면역)를 구분 반환. `Step4_Reveal`이 현재 `IsRevealed` 미소비 → 여기가 소비처.
  - `InputRequest` 선택지 필드(D2 이연): 유효 대상·지불가능 스킬. 정보 대응 요청(`InputPhase.Info`)에도 선택지 부착.
  - `ActionResolver.PreviewDamage` 예상피해 재사용.
- **캐릭터 스킬 설계 대주제**:
  - 정보형 고유행동 공격 겸용 확정 시 `InfoResponseSystem.TryApply`의 seam(`[캐릭터 스킬 설계 후 수렴]`)에 `ActionResolver` 경로 연결(`SetRevealed`·소모는 유지).
  - 연의 무기 태그별 고유행동 변형(§278) — 무기 시스템 별도.
  - `SkillData`에 공개 범위 세부 필드 추가(현재 `IsInfoAction` 자격 플래그만).
- **밸런스 데이터 대주제**: `ResponsePhaseSystem` AP 비용을 데이터 자산으로 이관.
- **5단계(통합 검증)**: `_Scratch/J_IntegrationProbe.cs`를 리플렉션 헬퍼 걷어내고 `Tests/` + asmdef로 승격 검토. **BuildOrder 회수로 헤드리스 컴파일 전제 해소됨** — I·J 프로브 모두 승격 가능.

---

## 9. 미해결 · 커밋 계획 · 세션 시작 전 수동 작업

### 커밋 계획(소주제 단위 최소 커밋. `_Scratch/` 제외)
1. **feat(J-1)**: Reveal 모델 통일 — `ActionKind`(Reveal 제거), `ActionCommand`(CreateReveal 제거), `SkillData`(IsInfoAction 추가).
2. **feat(J-2)**: 정보형 고유행동 자격판정·실행 — `InfoResponseSystem` 신설.
3. **feat(J-3)**: 정보 대응 배선 — `BattleFlowSystem`: Step5(void→IEnumerator)·ExecuteTurn(재-yield)·Step6(acted 게이트).
4. **fix**: `BuildOrder` tuple 요소명 명시화 — mono-mcs name inference 회수(I §7). **J와 분리.**

### 세션 시작 전 수동 작업(사용자)
1. 위 4커밋을 `dev`에 반영. `_Scratch/`(프로브·스텁) 제외.
2. **이 완료 인계문서를 프로젝트 Knowledge에 업로드.**
3. `main`을 `dev`와 동기화.

### 다음 착수 지점
후보: **UI 대주제**(정보 조회 API·선택지 필드 — 정보 공개의 자연스러운 소비처) / **캐릭터 스킬 설계**(정보형 고유행동 효과·연 무기 변형 확정, 이연분 회수) / **Bundle 4**(영지·성장·장비·보상·세이브). UI가 J 이연분(공개 범위 세부·L3 소비처)의 소비처라 순서상 자연스러우나, 착수 순서는 GPT 상위 지시·GDD 우선순위에 따름. UI 착수 시 헤드리스 검증 패러다임이 성립하지 않는 문제(실제 씬·프리팹 필요)를 먼저 다뤄야 함 — 이건 J 범위 밖에서 J 착수 시 이미 지적한 사항.
