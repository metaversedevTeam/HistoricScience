using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 노션 "AI-가상융합 서비스 개발자 경진대회 / TODO" 문서의 <튜토리얼 진행 순서>를 그대로 옮긴 단계 목록.
// 완료 판정은 게임 쪽의 공개 이벤트·상태만 읽어서 하므로, 게임 코드는 튜토리얼의 존재를 전혀 모른다.
public static class TutorialScenario
{
    // 강조 문구에 쓰는 호박색. 피그마 대화창의 "강조 상태"에 해당한다.
    private const string Accent = "<color=#FBBF24>";
    private const string AccentEnd = "</color>";

    // 명령 버튼 이름들. Citizen·Lab·Warehouse·HomeBase가 만드는 CommandData의 이름과 같아야 한다.
    private const string GatherCommand = "채집";
    private const string BuildCommand = "건물 짓기";
    private const string WorkbenchCommand = "작업대 열기";
    private const string WarehouseCommand = "창고 열기";
    private const string CodexCommand = "도감 열기";

    // 건물 선택 UI에서 고르게 할 대장간의 이름. Lab 프리팹의 건물 이름과 같아야 한다.
    private const string ForgeBuildingName = "대장간";

    // 채집 단계에서 캐게 할 돌 개수 (노션 문서의 "돌 3개를 채굴해보게 하기")
    private const int StoneGoal = 3;

    // 카메라 조작 단계를 마쳤다고 볼 누적 이동 거리(월드 단위)
    private const float CameraTravelGoal = 25f;

    // 휠 확대 단계를 마쳤다고 볼 누적 배율 변화량. CameraController의 확대 범위(0.5~2)에서 휠 서너 번에 해당한다.
    private const float ZoomChangeGoal = 0.25f;

    // 좀돌날 조합법이 요구하는 돌 개수. 재료가 모자랄 때 안내 문구를 덧붙이는 데만 쓴다.
    private const int MicrobladeStoneCost = 2;

    // 노션 문서의 순서대로 튜토리얼 단계를 만들어 돌려준다.
    public static IEnumerable<TutorialStep> Build()
    {
        var steps = new List<TutorialStep>();

        AddIntro(steps);
        AddUnitAndCameraControl(steps);
        AddGathering(steps);
        AddBiome(steps);
        AddBuilding(steps);
        AddWorkbench(steps);
        AddWarehouse(steps);
        AddHomeBase(steps);
        AddOutro(steps);

        return steps;
    }

    // 튜토리얼을 이미 들었는지 묻고, 들었다면 그대로 끝낸다.
    private static void AddIntro(List<TutorialStep> steps)
    {
        steps.Add(new TutorialAskStep(
            "안녕하세요! 이 땅을 함께 일굴 안내자입니다. 튜토리얼을 이미 들어보셨나요?",
            "예 (건너뛰기)",
            "아니요 (진행하기)",
            HandleIntroAnswered));
    }

    // 유닛과 카메라 조작법을 익히게 한다.
    private static void AddUnitAndCameraControl(List<TutorialStep> steps)
    {
        steps.Add(new TutorialTaskStep(
            new[] { "먼저 화면을 둘러보는 법입니다. 키보드로 카메라를 자유롭게 옮길 수 있어요." },
            runner => new TutorialStepStatus(
                $"{Accent}화살표 키{AccentEnd}를 눌러 카메라를 움직여 보세요.",
                runner.Progress.CameraTravel >= CameraTravelGoal)));

        steps.Add(new TutorialTaskStep(
            new[] { "가까이 들여다볼 수도, 멀리서 넓게 볼 수도 있습니다." },
            runner => new TutorialStepStatus(
                $"{Accent}마우스 휠{AccentEnd}을 굴려 화면을 확대하거나 축소해 보세요.",
                runner.Progress.ZoomChange >= ZoomChangeGoal)));

        steps.Add(new TutorialTaskStep(
            new[] { "이 땅에는 당신을 도울 시민이 있습니다." },
            runner => new TutorialStepStatus(
                $"개체를 {Accent}왼쪽 클릭{AccentEnd}하면 선택됩니다. 시민을 클릭해 선택해 보세요.",
                runner.Context.SelectedCitizen != null),
            runner => HighlightWorld(runner.Context.NearestCitizen, 1.4f)));

        steps.Add(new TutorialTaskStep(
            null,
            runner => new TutorialStepStatus(
                runner.Context.SelectedCitizen == null
                    ? $"먼저 {Accent}시민{AccentEnd}을 다시 선택해 주세요."
                    : $"시민을 선택한 채로 땅을 {Accent}오른쪽 클릭{AccentEnd}하면 그곳으로 걸어갑니다. 이동을 명령해 보세요.",
                runner.Progress.MoveOrdered),
            runner => HighlightWorld(runner.Context.SelectedCitizen, 1.4f, dim: false)));

        steps.Add(new TutorialTaskStep(
            new[] { "시민이 화면 밖으로 나가도 금방 찾을 수 있습니다." },
            runner => new TutorialStepStatus(
                $"{Accent}스페이스바{AccentEnd}를 누르고 있으면 카메라가 가장 가까운 시민에게 갑니다. 눌러 보세요.",
                Keyboard.current != null && Keyboard.current.spaceKey.isPressed)));
    }

    // 채집 모드로 돌을 캐게 한다.
    private static void AddGathering(List<TutorialStep> steps)
    {
        steps.Add(new TutorialTaskStep(
            new[] { "이제 재료를 캐 봅시다. 시민을 선택하면 화면 아래에 명령 버튼이 나타납니다." },
            runner => new TutorialStepStatus(
                runner.Commands.FindButton(GatherCommand) == null
                    ? $"먼저 {Accent}시민{AccentEnd}을 선택해 명령 버튼을 띄워 주세요."
                    : $"명령 버튼 중 {Accent}[채집]{AccentEnd}을 눌러 채집 모드를 시작하세요.",
                runner.Commands.WasClicked(GatherCommand)),
            runner => HighlightCommandOrCitizen(runner, GatherCommand)));

        // 돌이 어떻게 생겼는지 몰라 헤매지 않도록, 이 단계 내내 자원 소스의 모습을 대화창 옆에 띄워 둔다.
        steps.Add(new TutorialTaskStep(
            new[] { "채집 모드에서 캐고 싶은 자원을 왼쪽 클릭하면, 시민이 그쪽으로 걸어가 캐기 시작합니다." },
            HandleStoneStatus,
            runner => HighlightWorld(runner.Context.NearestStoneSource, 2f, dim: false),
            TutorialImageLibrary.StoneSource,
            "이렇게 생긴 자원이에요"));
    }

    // 바이옴 개념을 설명한다.
    private static void AddBiome(List<TutorialStep> steps)
    {
        steps.Add(new TutorialTalkStep(
            $"땅은 {Accent}바이옴{AccentEnd}으로 나뉘어 있습니다. 풀밭, 흙, 모래, 바위처럼 지형이 다르게 보이는 구역이지요.",
            "바이옴마다 나타나는 자원이 다릅니다. 찾는 재료가 보이지 않으면 다른 바이옴으로 카메라를 옮겨 보세요."));
    }

    // 건물 선택 UI를 열고 대장간을 짓게 한다.
    private static void AddBuilding(List<TutorialStep> steps)
    {
        steps.Add(new TutorialTaskStep(
            new[] { "재료를 모았으면 건물을 지을 차례입니다." },
            runner => new TutorialStepStatus(
                runner.Commands.FindButton(BuildCommand) == null
                    ? $"먼저 {Accent}시민{AccentEnd}을 선택해 명령 버튼을 띄워 주세요."
                    : $"{Accent}[건물 짓기]{AccentEnd}를 눌러 지을 건물을 골라 보세요.",
                runner.Commands.WasClicked(BuildCommand)),
            runner => HighlightCommandOrCitizen(runner, BuildCommand)));

        steps.Add(new TutorialTaskStep(
            new[]
            {
                $"목록에서 {Accent}대장간{AccentEnd}을 고르고 [건축 시작]을 누르세요.",
                "그다음 지을 자리를 왼쪽 클릭해 정하고 [건축] 명령을 내리면, 시민이 그 자리로 걸어가 건물을 세웁니다.",
            },
            HandleForgeStatus,
            runner => HighlightBuildingCard(runner, ForgeBuildingName)));
    }

    // 대장간의 작업대로 좀돌날을 만들게 한다.
    private static void AddWorkbench(List<TutorialStep> steps)
    {
        steps.Add(new TutorialTaskStep(
            new[] { "대장간에는 재료를 다듬는 작업대가 있습니다." },
            runner => new TutorialStepStatus(
                runner.Commands.FindButton(WorkbenchCommand) == null
                    ? $"{Accent}대장간{AccentEnd}을 클릭해 선택해 주세요."
                    : $"{Accent}[작업대 열기]{AccentEnd}를 눌러 작업대를 열어 보세요.",
                runner.Commands.WasClicked(WorkbenchCommand)),
            runner => HighlightCommandOrTarget(runner, WorkbenchCommand, runner.Context.Lab, 2.5f)));

        steps.Add(new TutorialTaskStep(
            new[]
            {
                "작업대는 재료를 놓은 <b>모양</b>대로 물건을 만듭니다. 왼쪽 창고 칸의 재료를 끌어다 가운데 격자에 놓으세요.",
                "잘못 놓았다면 격자의 칸을 클릭해 다시 비울 수 있습니다.",
            },
            HandleMicrobladeStatus));
    }

    // 창고를 짓고 보관 중인 재료를 확인하게 한다.
    private static void AddWarehouse(List<TutorialStep> steps)
    {
        steps.Add(new TutorialTaskStep(
            new[] { "모은 재료는 창고에서 한눈에 볼 수 있습니다. 앞에서 배운 대로 창고를 지어 봅시다." },
            HandleWarehouseBuildStatus,
            runner => HighlightCommandOnly(runner, BuildCommand, dim: false)));

        steps.Add(new TutorialTaskStep(
            null,
            runner => new TutorialStepStatus(
                runner.Commands.FindButton(WarehouseCommand) == null
                    ? $"{Accent}창고{AccentEnd}를 클릭해 선택해 주세요."
                    : $"{Accent}[창고 열기]{AccentEnd}를 눌러 창고를 열어 보세요.",
                runner.Commands.WasClicked(WarehouseCommand)),
            runner => HighlightCommandOrTarget(runner, WarehouseCommand, runner.Context.Warehouse, 2.5f)));

        steps.Add(new TutorialTalkStep(
            $"창고 목록에서 방금 만든 {Accent}좀돌날{AccentEnd}이 있는지 확인해 보세요. 확인했다면 이 대화창을 클릭해 주세요."));
    }

    // 근거지를 짓고 도감에서 조합법 힌트를 받아 보게 한다.
    private static void AddHomeBase(List<TutorialStep> steps)
    {
        steps.Add(new TutorialTaskStep(
            new[] { "마지막으로 근거지입니다. 근거지에서는 지금까지 알아낸 것들을 도감으로 볼 수 있습니다." },
            HandleHomeBaseBuildStatus,
            runner => HighlightCommandOnly(runner, BuildCommand, dim: false)));

        steps.Add(new TutorialTaskStep(
            null,
            runner => new TutorialStepStatus(
                runner.Commands.FindButton(CodexCommand) == null
                    ? $"{Accent}근거지{AccentEnd}를 클릭해 선택해 주세요."
                    : $"{Accent}[도감 열기]{AccentEnd}를 눌러 도감을 열어 보세요.",
                runner.Commands.WasClicked(CodexCommand)),
            runner => HighlightCommandOrTarget(runner, CodexCommand, runner.Context.HomeBase, 3f)));

        steps.Add(new TutorialTaskStep(
            new[] { "도감에는 아직 만들어 보지 못한 물건의 조합법을 한 칸씩 알려 주는 힌트가 있습니다." },
            runner => new TutorialStepStatus(
                $"아직 모으지 못한 아이템의 {Accent}[힌트 받기]{AccentEnd}를 눌러 조합법 힌트를 받아 보세요. 힌트에는 재료가 조금 듭니다.",
                runner.Progress.HintRevealed)));
    }

    // 마무리 인사를 하고 튜토리얼을 끝낸다.
    private static void AddOutro(List<TutorialStep> steps)
    {
        steps.Add(new TutorialTalkStep(
            "여기까지입니다. 이제 캐고, 만들고, 지으며 이 땅의 역사를 넓혀 보세요.",
            "도움이 필요하면 근거지의 도감을 열어 보세요. 그럼, 행운을 빕니다!"));
    }

    // 첫 질문의 답을 처리한다. 이미 들어 봤다면 튜토리얼을 그대로 끝낸다.
    private static void HandleIntroAnswered(TutorialRunner runner, bool hasPlayedBefore)
    {
        if (hasPlayedBefore)
            runner.Finish();
    }

    // 돌 채집 진행도를 문구와 판정으로 만든다. 돌 아이템을 찾지 못했으면 막히지 않도록 그냥 통과시킨다.
    private static TutorialStepStatus HandleStoneStatus(TutorialRunner runner)
    {
        ItemData stone = runner.Context.Stone;
        if (stone == null)
            return TutorialStepStatus.Done("자원을 캐 보세요.");

        int gained = runner.Progress.GetGained(stone);
        return new TutorialStepStatus(
            $"{Accent}돌{AccentEnd}을 {StoneGoal}개 캐 보세요. ({Mathf.Min(gained, StoneGoal)}/{StoneGoal})",
            gained >= StoneGoal);
    }

    // 대장간 건축 진행도를 문구와 판정으로 만든다.
    private static TutorialStepStatus HandleForgeStatus(TutorialRunner runner)
    {
        bool built = runner.Context.Lab != null;
        return new TutorialStepStatus(
            $"{Accent}대장간{AccentEnd}을 지어 보세요. {DescribeStone(runner)}",
            built);
    }

    // 창고 건축 진행도를 문구와 판정으로 만든다.
    private static TutorialStepStatus HandleWarehouseBuildStatus(TutorialRunner runner)
    {
        bool built = runner.Context.Warehouse != null;
        return new TutorialStepStatus(
            $"재료를 모아 {Accent}창고{AccentEnd}를 지어 보세요. {DescribeStone(runner)}",
            built);
    }

    // 근거지 건축 진행도를 문구와 판정으로 만든다.
    private static TutorialStepStatus HandleHomeBaseBuildStatus(TutorialRunner runner)
    {
        bool built = runner.Context.HomeBase != null;
        return new TutorialStepStatus(
            $"재료를 모아 {Accent}근거지{AccentEnd}를 지어 보세요. {DescribeStone(runner)}",
            built);
    }

    // 좀돌날 제작 진행도를 문구와 판정으로 만든다. 좀돌날을 찾지 못했으면 막히지 않도록 그냥 통과시킨다.
    private static TutorialStepStatus HandleMicrobladeStatus(TutorialRunner runner)
    {
        ItemData microblade = runner.Context.Microblade;
        if (microblade == null)
            return TutorialStepStatus.Done("작업대에서 무언가를 만들어 보세요.");

        bool made = runner.Progress.GetGained(microblade) > 0;

        // 앞에서 대장간을 지으며 돌을 다 썼을 수 있으므로, 모자랄 때는 다시 캐 오라고 알려 준다.
        int stone = runner.Context.CountOwned(runner.Context.Stone);
        string help = stone >= MicrobladeStoneCost ? string.Empty : " 돌이 모자라면 작업대를 닫고 더 캐 오세요.";

        return new TutorialStepStatus(
            $"{Accent}돌 2개를 세로로 나란히{AccentEnd} 놓고 [제작]을 누르면 좀돌날이 만들어집니다. {DescribeStone(runner)}{help}",
            made);
    }

    // 지금 들고 있는 돌 개수를 알리는 짧은 문구를 만든다. 재료가 모자랄 때 더 캐야 한다는 것을 알 수 있게 한다.
    private static string DescribeStone(TutorialRunner runner)
    {
        ItemData stone = runner.Context.Stone;
        return stone != null ? $"(보유한 돌: {runner.Context.CountOwned(stone)}개)" : string.Empty;
    }

    // 명령 버튼이 떠 있을 때만 그 버튼을 강조한다. 재료를 모으거나 자리를 고르는 동안에는 아무것도 강조하지 않는다.
    private static TutorialHighlightRequest? HighlightCommandOnly(TutorialRunner runner, string commandName, bool dim = true)
    {
        RectTransform button = runner.Commands.FindButton(commandName);
        return button != null ? TutorialHighlightRequest.Ui(button, dim) : (TutorialHighlightRequest?)null;
    }

    // 명령 버튼이 떠 있으면 그 버튼을, 아직이면 가장 가까운 시민을 강조한다.
    private static TutorialHighlightRequest? HighlightCommandOrCitizen(TutorialRunner runner, string commandName, bool dim = true)
    {
        return HighlightCommandOrTarget(runner, commandName, runner.Context.NearestCitizen, 1.4f, dim);
    }

    // 명령 버튼이 떠 있으면 그 버튼을, 아직이면 명령을 내릴 대상 오브젝트를 강조한다.
    private static TutorialHighlightRequest? HighlightCommandOrTarget(TutorialRunner runner, string commandName, Component target, float radius, bool dim = true)
    {
        RectTransform button = runner.Commands.FindButton(commandName);
        if (button != null)
            return TutorialHighlightRequest.Ui(button, dim);

        return HighlightWorld(target, radius, dim);
    }

    // 건물 선택 UI가 열려 있으면 지정한 건물의 카드를 강조한다. 목록을 읽어야 하므로 화면을 어둡게 덮지 않는다.
    private static TutorialHighlightRequest? HighlightBuildingCard(TutorialRunner runner, string buildingName)
    {
        BuildingCardUI card = runner.Context.FindBuildingCard(buildingName);
        return card != null ? TutorialHighlightRequest.Ui((RectTransform)card.transform, dim: false) : (TutorialHighlightRequest?)null;
    }

    // 월드 오브젝트가 살아 있으면 그것을 강조하는 요청을 만든다.
    private static TutorialHighlightRequest? HighlightWorld(Component target, float radius, bool dim = true)
    {
        return target != null ? TutorialHighlightRequest.World(target.transform, radius, dim) : (TutorialHighlightRequest?)null;
    }
}
