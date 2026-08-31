# 인계문서 — 4단계 구현 B대주제: AP · 행동 명령서 · 적 intent · 글로벌 턴 루프 골격

- 프로젝트: 라플라스 전기 1부 (Unity / C#)
- 단계: 4단계(구현)
- 대주제: **B — AP + 턴 골격** (공정표 §3)
- 상태: **완료** — 대주제 동작 검증 통과. 채팅 전환 가능 지점.
- 선행: `인계문서_4단계_A대주제_데이터_유닛뼈대` (런타임 유닛·데이터 뼈대), `공정표_3단계`, `인계문서_2단계_묶음1`(설계 진실원), `인계문서_2단계_묶음2`(도발 삭제 반영)
- **SSOT = GitHub `First-Story` / `dev` 브랜치.** 코드 진실은 레포. 본 문서와 표기 충돌 시 레포 우선.
- 구현 원칙: 코드는 채팅 전달(파일 미기록 방침), 소주제 = 커밋 최소 단위, 프로브는 검증용 폐기물(커밋 제외).

---

## 0. 이번 대주제 범위·경계

- 담은 것: AP 회복/소모/판정, 행동 명령서 구조(팩토리), 적 intent 모양·보관·공개, 글로벌 턴 루프 9단계 골격 + 7단계 실행 골조(정렬 1회·유효성 재검사) + 더미 intent.
- **골격(스텁)으로 남긴 것**: 5·6단계(정보·방어 대응), 7단계 실행 내부(7c), 8·9단계(턴종료·판정), 붕괴 취소 재검사. 전부 로그/인터페이스 자리만 — 묶음2·3이 채움.
- 성격: **코드 존재 → 실제 동작 검증 수행함**(설계문서 묶음1의 "정합성 검증"과 다름).

---

## 1. 산출물

### 커밋된 파일 (5개, 4소주제)

| 소주제 | 파일 | 역할 |
|---|---|---|
| B-1 | `Assets/Scripts/Systems/APSystem.cs` | AP 회복·판정·소모 (static, stateless) |
| B-2 | `Assets/Scripts/Data/Enums/ActionKind.cs` | 행동 종류 enum (7종) |
| B-2 | `Assets/Scripts/Core/ActionCommand.cs` | 행동 명령서 (순수 데이터 + 팩토리) |
| B-3 | `Assets/Scripts/Core/EnemyIntent.cs` | 한 적의 이번 턴 의도 (불변 데이터) |
| B-3 | `Assets/Scripts/Systems/IntentSystem.cs` | 적 intent 일괄 보관·공개 (인스턴스, 상태 있음) |
| B-4 | `Assets/Scripts/Systems/IActionExecutor.cs` | 실행 구멍 인터페이스 (묶음2 구현 예정) |
| B-4 | `Assets/Scripts/Systems/BattleFlowSystem.cs` | 턴 루프 9단계 소유·조율 (인스턴스) |

- 신규 폴더: `Assets/Scripts/Systems/` (규약 7절 기능별 분리).

### 폐기물 (커밋 제외)

- `APSystemProbe.cs` / `ActionCommandProbe.cs` / `IntentSystemProbe.cs` / `BattleFlowProbe.cs` — 검증 후 삭제.

### 별도 커밋 (B와 무관, 선행 정리)

- `EnemyUnitData.cs` 오타 수정 `Guage → Gauge` (A대주제 파일). B-1 커밋에 섞지 말고 단독 커밋 권장.
  - **주의**: 이 이름을 실제로 쓰는 곳은 D대주제(Break/CrackSystem). 수정 후 `MaxBreakGauge`/`MaxCrackGauge`로 참조.

---

## 2. 핵심 구조

```
[턴 진행 조율]
BattleFlowSystem  // 9단계 순차 소유. 각 단계 하위 시스템/구멍 호출. 인스턴스(턴번호 상태 보유)
  ├─ APSystem         // static. AP 회복/판정/소모. 상태 없음
  ├─ IntentSystem     // 인스턴스. 적별 intent + 정보확인 플래그 보관·공개
  ├─ IActionExecutor  // 실행 구멍. 명령서 받아 실행(묶음2 ActionResolver가 구현)
  └─ Func<string,SkillData> // skillId 해석기 주입(스킬DB/G가 대체)

[데이터]
ActionKind    // 행동 종류 enum 7종
ActionCommand // { Kind, Actor, Target, Skill, Direction } 순수 데이터. 팩토리 생성. 실행 로직 없음
EnemyIntent   // { Skill, Target } 불변. 방향 등은 Skill에서 파생(중복 저장 안 함)
```

각 책임 1줄:
- **APSystem**: "얼마 회복·소모, 지불 가능한가" 판단만. AllyUnit만 상대(적 AP 없음).
- **ActionCommand**: 아군 실행행동 + 대응행동을 한 형태로 통일. 종류는 enum, 실행은 밖(BattleFlowSystem).
- **EnemyIntent**: 이번 턴 한 적의 의도(스킬+대상). 생성 시 통째 확정, 안 바뀜.
- **IntentSystem**: 모든 적 intent를 한 곳에 모아 순회 통로 제공 + 공개 수준(정보확인) 관리.
- **BattleFlowSystem**: 9단계 배선 + 7단계 정렬·재검사 루프 소유. 구멍은 인터페이스/로그로만 호출.

---

## 3. 공개 인터페이스 (다음 단계 의존 계약 — SSOT 시그니처)

### APSystem (static)
```csharp
static void RecoverAll(IReadOnlyList<AllyUnit> allies)   // 전투불능 스킵 내부 처리
static bool CanAfford(AllyUnit ally, int cost)           // 답만. 차단은 호출자
static void Consume(AllyUnit ally, int cost)             // 확정 1회. cost<0 예외
```

### ActionKind (enum)
`UniqueAction · Skill · Item · DirectionDefense · Protection · Reveal · EndTurn`
- **도발(Taunt) 없음** (묶음2 삭제 반영). **Reveal(정보확인) 포함** (대응행동도 명령서로 통일).

### ActionCommand
```csharp
ActionKind Kind { get; }  BattleUnit Actor { get; }  BattleUnit Target { get; }
SkillData Skill { get; }  AttackDirection Direction { get; }   // Direction = 방향방어 전용
// 팩토리 (생성자 private):
static ActionCommand CreateUnique(actor, skill, target)
static ActionCommand CreateSkill(actor, skill, target)
static ActionCommand CreateItem(actor, item, target)
static ActionCommand CreateEndTurn(actor)
static ActionCommand CreateDirectionDefense(actor, direction)   // None 금지
static ActionCommand CreateProtection(protector, protectee)
static ActionCommand CreateReveal(actor, target)
```
- **`Direction`은 방향방어 자세 전용**. 공격 방향의 진실원은 `Skill.Direction`(고유행동 포함). 명령서에 공격방향 중복 저장 안 함.

### EnemyIntent
```csharp
SkillData Skill { get; }  BattleUnit Target { get; }   // Target null 허용
EnemyIntent(SkillData skill, BattleUnit target)        // skill null 예외
```

### IntentSystem (인스턴스)
```csharp
void SetIntent(EnemyUnit enemy, EnemyIntent intent)    // enemy·intent null 예외
void ClearAll()                                        // intent + 플래그 동시 초기화
EnemyIntent GetIntent(EnemyUnit enemy)                 // 없으면 null
IReadOnlyDictionary<EnemyUnit,EnemyIntent> AllIntents  // 전체 순회 (순서 비보장 — §6 참조)
bool IsRevealed(EnemyUnit enemy)
void SetRevealed(EnemyUnit enemy)
```

### IActionExecutor
```csharp
void Execute(ActionCommand command)   // 묶음2 ActionResolver(12스텝)가 구현
```

### BattleFlowSystem (인스턴스)
```csharp
BattleFlowSystem(IReadOnlyList<AllyUnit> allies, IReadOnlyList<EnemyUnit> enemies,
                 IntentSystem intentSystem, IActionExecutor executor,
                 Func<string,SkillData> skillResolver)   // 인자 전부 null 예외
void ExecuteTurn()   // 한 글로벌 턴 = 9단계
int TurnNumber { get; }
```

---

## 4. 이번 세션 확정 설계 결정 (다음 세션 참고)

- **SSOT 네이밍 확정**(레포 우선): `SetAP`/`CurrAP`(설계문서 SetAp/CurrentAp 아님), 유닛 접근자 `BehaviorPatternID`(데이터층 `BehaviorPatternId`와 다름), `CurrBreakOrCrackGauge`/`SetGauge`, `DamageCoefficient` 류. **EnemyUnit에 AP 필드 없음.**
- **ActionKind 7종**: 도발 out(묶음2), 정보확인 in(묶음1 3-D + "대응·실행 명령서 통일" 설계).
- **`ActionCommand.Direction` = 방향방어 전용**: 공격 방향은 `Skill.Direction`이 진실원. 명령서 중복 저장 시 진실원 이중화 버그라 안 담음.
- **정보확인 플래그 = IntentSystem 소유**(intent 밖 HashSet): intent를 불변으로 유지. 데이터(의도) vs UI상태(공개여부) 층 분리.
- **EnemyIntent.Target = BattleUnit**(AllyUnit 아님): 적의 회복·보호막 대상이 아군 적일 수 있음. ActionCommand.Target과 타입 일치.
- **AP 필터 = 안A**: `RecoverAll`이 전체 아군 받아 내부에서 IsIncapacitated 스킵. 생존 정의를 시스템 한 곳에.
- **정렬 tie-break**: 참여자 = 전체 아군 → 전체 적 순 이어붙여 슬롯 인덱스 부여. 유효속도 내림차순, 동률이면 슬롯 인덱스 오름차순. → **동률 시 아군이 적보다 먼저**, 완전 결정론.
- **정렬 1회**(결정A): 7단계 진입 시 1회, 그 순서로 끝까지. 턴 중 둔화는 다음 턴(GDD 5.12).
- **순서 고정 + 재검사 스킵**(결정B): 리스트 순회 중 수정 안 함. 순번 왔을 때 "아직 유효?"만 물어 스킵.
- **구멍 추상화 수위**: 실행 구멍만 인터페이스(`IActionExecutor`). 아군 입력은 동기 스텁(자동 차례종료) — UI 붙을 때 갈아엎을 자리라 조기 추상화 회피.
- **skillId→SkillData = 해석기 주입**(`Func`): 공용 스킬DB 없어 주입으로 뚫음. 스킬DB/G가 대체.

---

## 5. 검증 결과 (대주제 동작 검증 — 통과)

프로브로 로그 기반 확인. 4소주제 전부 기대 로그 일치.

| 소주제 | 확인 항목 | 결과 |
|---|---|---|
| B-1 | AP 절삭 0→2→4→6→6 (첫턴 특례 없음) | ✓ |
| B-1 | CanAfford 양쪽(6/cost4 true, 2/cost4 false) · Consume 후 2 | ✓ |
| B-1 | 전투불능 아군 회복 스킵 | ✓ |
| B-1 | Consume 음수 cost 예외(리뷰 C 반영분) | ✓ |
| B-2 | 종류별 팩토리 필드 채움 정상 | ✓ |
| B-2 | 잘못된 조합 예외(None 방향방어 / null 스킬) | ✓ |
| B-3 | 등록·조회·미등록 null · 전체순회 수 | ✓ |
| B-3 | 정보확인 플래그 독립·intent 불변 · ClearAll 동시 초기화 | ✓ |
| B-3 | SetIntent null 예외(리뷰 E 반영분) | ✓ |
| B-4 | 9단계 순서 1→9 출력 | ✓ |
| B-4 | 더미 intent 부여 · 정렬 결정론 · 동률 아군우선 tie-break | ✓ |
| B-4 | 턴2 AP 추가 절삭 · 전투불능 적 intent 미부여 + 실행 스킵 | ✓ |

- **검증 통과 → 대주제 완결. C대주제(한 방 실행 파이프) 진입 조건 충족.**

---

## 6. 미해결 · 기술부채 · 다음 착수 지점

### 부채 — 고순위 (하위 대주제 진입 전 인지 필수)

- **[A] `BattleFlowSystem`이 `UnityEngine`에 의존**(Debug.Log): 나머지 코어 4파일은 `System.*`만 씀. 이 프로젝트 정체성(예상=실제 결정론)의 순수 테스트(Unity 밖 헤드리스)를 하려면 코어가 UnityEngine-free여야 함 — 턴 루프가 거기서 막힘. + 빌드에서 로그 인자 문자열 조립 비용. **회수: 로깅을 `Action<string>`/`IBattleLog` 주입으로 분리.** UI/이벤트 채널 도입 시점.
- **[B] `IntentSystem.AllIntents` 딕셔너리 순회 순서 비보장**: C# 계약상 Dictionary/HashSet 열거 순서 미보장. 지금은 로그만이라 무해. **묶음2 대응단계가 `AllIntents`를 훑어 순서 있는 결정을 내리면 비결정론 발생 → "무작위 절대금지" 위반.** 회수: 하위 로직은 순서를 `_enemies` 슬롯 인덱스에서 파생. `AllIntents` 순서에 의존 금지.

### 부채 — 중·저순위

- **[D] AP 밸런스 상수 하드코딩**(`_apGainPerTurn`, `_maxAP`): GDD 9.1 "밸런스 값 데이터 분리"와 편차. 현재 config 시스템 없어 static readonly가 매직넘버보단 나음. 회수: BattleConfig SO 등 밸런스 에셋 생기면 이관.
- **[참조키] `IntentSystem` Dictionary 키 = `EnemyUnit` 참조**: 전투 중 무해. **저장/로드(묶음4) 시 참조 깨져 재바인딩 필요.** EnemyUnit에 안정 ID 없어 참조가 현 최선. 묶음4 저장 설계 때 회수.
- **[아군입력] 7단계 아군 = 동기 자동 차례종료 스텁**: 실제 UI 붙으면 "입력 대기로 루프 정지" → 코루틴/async 전환으로 BattleFlowSystem 재작성. `IActionExecutor`와 별개.
- **[해석기] skillId→SkillData = 임시 주입**: 공용 스킬DB(레지스트리) 필요. 프로브는 임시 딕셔너리로 제공 중.

### 구멍 (hook) — 하위 대주제가 채울 자리

- **C대주제**: 7단계 실행 내부(7c) — `IActionExecutor.Execute` 구현(대상결정→회피→방향→피해/회복→사망→붕괴→상태). CombatCalculator "예상=실제" 열쇠.
- **D대주제**: 붕괴 취소 재검사(`IsStillValid`의 "붕괴로 취소" 자리, 지금 항상 통과) / 보호로 대상 갈아끼움 / 방향방어·자세 / 회피.
  - 추가: **대응한 아군은 7단계 정렬에서 제외**해야 함(묶음2). 지금 골조는 전 아군 정렬 포함 → 대응 시스템 붙을 때 제외 로직 필요.
- **E대주제**: 8단계(상태이상 틱·만료).
- **F대주제**: 9단계(전멸·승리·웨이브 전환) + 전투불능 전이.
- **G대주제**: 3단계(`Step3_AssignEnemyIntent`) — 더미 intent를 진짜 Decide 결과로 **통째 교체**. IntentSystem·명령서·파이프는 불변(intent 모양 동일).
  - 5단계 정보 대응 → `IntentSystem.SetRevealed` 호출부 배선.

### 다음 착수 지점

- **C대주제 — 한 방 실행 파이프** (공정표 §4). 의존: B(완료).
  - C-1 CombatCalculator(순수) → C-2 Damage/HealingSystem → C-3 DirectionSystem → C-4 TargetingSystem 기본 → C-5 ActionResolver 12스텝 뼈대.
  - C의 실체 = 본 문서 `IActionExecutor.Execute` 구멍을 채우는 것.
- 착수 조건: 본 인계문서 + 묶음2 설계문서가 Knowledge에 있어야 맥락 연결.
- **세션 전 수동 작업 2가지**(지침 8절): ① B-1~B-4 + 오타수정 커밋(GitHub SSOT 갱신) ② 본 인계문서 Knowledge 업로드.
