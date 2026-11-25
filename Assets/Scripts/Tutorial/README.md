# 튜토리얼

노션 `AI-가상융합 서비스 개발자 경진대회 / TODO` 문서의 <튜토리얼 진행 순서>를 구현한 시스템입니다.
대화창은 메인 메뉴·일시정지 화면과 같은 사각형 디자인(모서리 없는 어두운 판, 얇은 테두리, 왼쪽 강조 막대, 하늘·초록·주황 세 색 막대, 영문 캡션, 사각 표식)을 따릅니다.
강조색은 지금 하는 일을 나타냅니다: 대사 읽기는 하늘색, 질문은 초록, 목표 수행은 주황.
(스킵 버튼은 따로 두지 않습니다. 튜토리얼을 건너뛰는 길은 첫 질문의 "예"뿐입니다.)

## 통째로 걷어내는 방법

1. `Assets/Scripts/Tutorial` 폴더를 삭제한다.
2. `Assets/Resources/Tutorial` 폴더를 삭제한다. (대화창에 띄울 그림을 등록해 둔 에셋 하나만 들어 있다)
3. `Assets/Scripts/UI/MainMenu/MapSelectUI.cs`에서 `TutorialSession`을 부르는 두 줄(`MarkNewMapCreated`·`MarkExistingMapLoaded`)을 지운다.

이 세 곳 말고는 어떤 코드도 튜토리얼을 참조하지 않습니다. 씬·프리팹에도 아무것도 심어 두지 않았고,
그림도 원래 있던 자리를 참조만 하므로 아트 에셋은 하나도 건드리지 않습니다.

## 왜 프리팹이 아니라 코드로 UI를 만드나

튜토리얼 UI를 프리팹으로 만들면 그 프리팹과 씬 배치까지 함께 지워야 하고, 프리팹이 살아 있는 동안에는
씬이 튜토리얼을 참조하게 됩니다. 그래서 `TutorialUIBuilder`가 일시정지 화면과 같은 판·테두리(`RectOutlineGraphic`)·선택 칸을
실행 중에 조립합니다. 그림 스프라이트 없이 단색 사각형만 쓰며, 색과 치수는 모두 `TutorialTheme` 한 곳에 모여 있습니다.

## 그림은 어떻게 넣나

코드로 만든 UI에는 인스펙터로 그림을 연결할 곳이 없습니다. 그래서 `Assets/Resources/Tutorial/튜토리얼 이미지.asset`
(`TutorialImageLibrary`) 하나만 두고, 여기에 프로젝트에 이미 있는 스프라이트를 연결해 실행 중에 읽어 씁니다.
그림을 복사하지 않고 참조만 하므로 원본을 고치면 튜토리얼에도 그대로 반영됩니다.

| 칸 | 쓰이는 곳 |
|---|---|
| `_stoneSource` | 돌 3개 채집 단계에서 대화창 옆에 "이렇게 생긴 자원이에요" 카드로 띄운다 (`Art/Sprites/건물 아이콘/StoneSource Image`) |

다른 단계에도 그림을 붙이려면 이 에셋에 칸을 하나 늘리고, `TutorialScenario`에서 해당 `TutorialTaskStep`의
`image`·`imageCaption` 인자로 넘기면 됩니다.

## 구성

| 파일 | 하는 일 |
|---|---|
| `TutorialSession` | 유일한 진입점. 맵을 새로 만든 세션인지 받아 두고, 인게임 씬이 열리면 실행기를 붙인다 |
| `TutorialRunner` | 단계를 순서대로 진행시키고, 대화창의 수명을 관리한다 |
| `TutorialScenario` | 노션 문서 순서대로 단계를 만든다. 문구와 완료 조건은 모두 여기 있다 |
| `TutorialContext` | 씬의 매니저·유닛·건물을 찾아 두고 조회를 캐싱한다 |
| `TutorialProgressWatcher` | 마우스 시점 회전, WASD로 걸은·Shift로 달린 거리, 휠 확대, 아이템 획득, 힌트 공개를 지켜본다 |
| `TutorialInteractionWatcher` | 시민 앞 대상과 명령 목록(휠로 고르고 F로 실행)을 읽어, 골라진 명령과 실행된 명령을 기록한다 |
| `Steps/*` | 대사 단계·질문 단계·목표 단계 |
| `UI/*` | 일시정지 화면 디자인을 코드로 옮긴 대화창 |

## 진행 판정을 어떻게 하나

게임 코드는 튜토리얼을 모르므로, 완료 판정은 이미 공개되어 있는 이벤트와 상태만 읽어서 합니다.

- 시점 조작 — 카메라 회전 각도(좌우·위아래) 누적
- 걷기·달리기 — WASD 입력(`PlayerManager.MoveDirection`·`IsRunning`)이 있는 동안 플레이어 시민(`CameraController.FollowTarget`)이 움직인 거리
- 대상 앞에 서기 — 플레이어 시민의 `Citizen.InteractionTarget`
- 명령 실행(채집·작업대·창고·도감 열기) — F가 눌린 프레임에 `InteractionCommandListUI.SelectedIndex`가 가리키던 명령. 대상의 명령 이름은 시민과 같은 규칙(채집 먼저, 그 뒤 `ICommandable` 명령)으로 다시 만든다. 여는 명령은 해당 UI가 열렸는지도 함께 본다
- 건물 선택 창 / 자리 지정 — 씬에 `BuildingSelectUI`가 떠 있는지, `BuildingPlacementController.ActiveCount`
- 돌 채집 · 좀돌날 제작 — `ResourceInventory.OnAddItemAt`
- 대장간 · 창고 · 근거지 건축 — 씬에 `Lab` / `Warehouse` / `HomeBase`가 생겼는지
- 조합법 힌트 — `ItemCodex.OnHintRevealed`

## 다른 UI 위에 대화창 띄우기

대화창은 튜토리얼이 끝날 때까지 떠 있어야 하고 ESC로 닫히면 안 되므로 UIManager의 개폐 대상이 아니라 캔버스 루트에 직접 붙고,
매 프레임 형제 순서만 맨 뒤로 맞춰 건물 선택 UI나 작업대 UI가 떠 있어도 그 위에 그려집니다.

## 에디터에서 시험하기

맵을 새로 만들지 않고 확인하려면 메뉴 `Tools > 튜토리얼 > 다음 인게임 진입에서 튜토리얼 시작`을 누른 뒤
인게임 씬을 실행하면 그 한 번만 튜토리얼이 시작됩니다.

## 손볼 만한 값

- 진행 순서와 문구: `TutorialScenario`
- 색·크기·대화창 위치: `TutorialTheme`
- 시점 회전·걷기·달리기·휠·채집 목표치: `TutorialScenario`의 상수

## 3인칭에서 대사 넘기기

3인칭 카메라는 커서를 화면 가운데에 잠그므로 대화창을 클릭할 수 없습니다. 그래서 `TutorialRunner`가
대사는 Space·Enter로 넘기고, 첫 질문의 예·아니요는 1·2 키로 답하게 합니다. Alt를 누르거나 창고·작업대처럼
조작을 막는 창이 열려 커서가 풀려 있을 때는 예전처럼 클릭해도 됩니다.
