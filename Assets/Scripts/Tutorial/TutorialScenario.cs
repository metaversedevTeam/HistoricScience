using System.Collections.Generic;
using UnityEngine;

// 3인칭 조작(마우스로 시점, WASD로 걷기, 대상 앞에서 휠로 명령을 고르고 F로 실행, B로 건물 선택)에 맞춘 튜토리얼 단계 목록.
// 진행 순서는 노션 "AI-가상융합 서비스 개발자 경진대회 / TODO" 문서의 <튜토리얼 진행 순서>를 따른다.
// 완료 판정은 게임 쪽의 공개 이벤트·상태만 읽어서 하므로, 게임 코드는 튜토리얼의 존재를 전혀 모른다.
public static class TutorialScenario
{
    // 키·대상 이름 같은 강조 문구에 쓰는 하늘색. 일시정지 화면 키 안내("ESC")와 같은 색이다.
    private const string Accent = "<color=#38BDF8>";
    private const string AccentEnd = "</color>";

    // 건물이 띄우는 명령 이름들. Lab·Warehouse·HomeBase가 만드는 CommandData의 이름과 같아야 한다.
    private const string WorkbenchCommand = "작업대 열기";
    private const string WarehouseCommand = "창고 열기";
    private const string CodexCommand = "도감 열기";
    private const string DemolishCommand = "철거";

    // 건물 선택 UI에서 고르게 할 건물 이름들. 각 프리팹의 건물 이름과 같아야 한다.
    private const string ForgeBuildingName = "대장간";
    private const string WarehouseBuildingName = "창고";
    private const string HomeBaseBuildingName = "근거지";

    // 시점 조작 단계를 마쳤다고 볼, 마우스로 돌린 누적 각도(도). 반 바퀴쯤 둘러보면 된다.
    private const float LookAngleGoal = 180f;

    // 걷기 단계를 마쳤다고 볼 누적 이동 거리(m)
    private const float WalkDistanceGoal = 8f;

    // 달리기 단계를 마쳤다고 볼 누적 달린 거리(m)
    private const float RunDistanceGoal = 6f;

    // 휠 확대 단계를 마쳤다고 볼 누적 배율 변화량. CameraController의 확대 범위(0.5~2)에서 휠 서너 번에 해당한다.
    private const float ZoomChangeGoal = 0.25f;

    // 채집 단계에서 캐게 할 돌 개수 (노션 문서의 "돌 3개를 채굴해보게 하기")
    private const int StoneGoal = 3;

    // 좀돌날 조합법이 요구하는 돌 개수. 재료가 모자랄 때 안내 문구를 덧붙이는 데만 쓴다.
    private const int MicrobladeStoneCost = 2;

    // 노션 문서의 순서대로 튜토리얼 단계를 만들어 돌려준다.
    public static IEnumerable<TutorialStep> Build()
    {
        var steps = new List<TutorialStep>();

        AddIntro(steps);
        AddLookAndMove(steps);
        AddGathering(steps);
        AddBiome(steps);
        AddBuilding(steps);
        AddWorkbench(steps);
        AddWarehouse(steps);
        AddHomeBase(steps);
        AddOutro(steps);

        return steps;
    }

    // 튜토리얼을 이미 들었는지 묻고, 들었다면 그대로 끝낸다. 커서가 잠겨 있으므로 숫자 키로 답하게 한다.
    private static void AddIntro(List<TutorialStep> steps)
    {
        steps.Add(new TutorialAskStep(
            "안녕하세요! 이 땅을 함께 일굴 안내자입니다. 튜토리얼을 이미 들어보셨나요?",
            "예 (건너뛰기)",
            "아니요 (진행하기)",
            HandleIntroAnswered));
    }

    // 시점 돌리기, 걷기, 달리기, 휠 확대, Alt 커서 풀기를 차례로 익히게 한다.
    private static void AddLookAndMove(List<TutorialStep> steps)
    {
        steps.Add(new TutorialTaskStep(
            new[]
            {
                "이 땅에서 당신은 시민 한 사람이 되어 직접 걸어 다닙니다.",
                $"제 말은 {Accent}Space{AccentEnd}나 {Accent}Enter{AccentEnd}로 넘길 수 있어요. 먼저 주위를 둘러볼까요?",
            },
            runner => new TutorialStepStatus(
                $"{Accent}마우스{AccentEnd}를 움직여 주위를 둘러보세요.",
                runner.Progress.LookAngle >= LookAngleGoal)));

        steps.Add(new TutorialTaskStep(
            null,
            runner => new TutorialStepStatus(
                $"{Accent}W·A·S·D{AccentEnd}로 걸어 보세요. W는 지금 화면이 바라보는 쪽입니다. {DescribeDistance(runner.Progress.WalkDistance, WalkDistanceGoal)}",
                runner.Progress.WalkDistance >= WalkDistanceGoal)));

        steps.Add(new TutorialTaskStep(
            new[] { "먼 곳까지 걸어가기엔 다리가 아프지요." },
            runner => new TutorialStepStatus(
                $"{Accent}Shift{AccentEnd}를 누른 채 걸으면 달립니다. 달려 보세요. {DescribeDistance(runner.Progress.RunDistance, RunDistanceGoal)}",
                runner.Progress.RunDistance >= RunDistanceGoal)));

        steps.Add(new TutorialTaskStep(
            new[] { "카메라를 시민 가까이 당기거나, 멀리 물려 넓게 볼 수도 있습니다." },
            runner => new TutorialStepStatus(
                $"{Accent}마우스 휠{AccentEnd}을 굴려 보세요. 앞에 명령 목록이 떠 있으면 휠이 명령 고르기에 쓰이니, 빈 곳을 보고 굴려 주세요.",
                runner.Progress.ZoomChange >= ZoomChangeGoal)));

        steps.Add(new TutorialTalkStep(
            $"평소에는 마우스 커서가 화면 가운데에 숨어 있습니다. 화면을 클릭해야 할 때는 {Accent}Alt{AccentEnd}를 누르고 있으면 커서가 나타나요.",
            "창고나 작업대처럼 창이 열려 있을 때는 커서가 저절로 나타나니 걱정하지 않으셔도 됩니다."));
    }

    // 돌 앞으로 걸어가 명령 목록을 띄우고, F로 채집해 돌을 모으게 한다.
    private static void AddGathering(List<TutorialStep> steps)
    {
        // 돌이 어떻게 생겼는지 몰라 헤매지 않도록, 채집 단계 내내 자원 소스의 모습을 대화창 옆에 띄워 둔다.
        steps.Add(new TutorialTaskStep(
            new[] { "이제 재료를 캐 봅시다. 자원이나 건물 바로 앞에 서면, 그 옆에 할 수 있는 일이 명령 목록으로 나타납니다." },
            runner => new TutorialStepStatus(
                $"{Accent}돌{AccentEnd} 앞으로 걸어가 명령 목록을 띄워 보세요.",
                runner.Interaction.HasCommand(TutorialInteractionWatcher.GatherCommand)),
            TutorialImageLibrary.StoneSource,
            "이렇게 생긴 자원이에요"));

        steps.Add(new TutorialTaskStep(
            new[] { $"목록에서 {Accent}[채집]{AccentEnd}이 골라진 채로 {Accent}F{AccentEnd}를 누르면 시민이 캐기 시작합니다. 다 캘 때까지 걷지 말고 기다려 주세요." },
            HandleStoneStatus,
            TutorialImageLibrary.StoneSource,
            "이렇게 생긴 자원이에요"));
    }

    // 바이옴 개념을 설명한다.
    private static void AddBiome(List<TutorialStep> steps)
    {
        steps.Add(new TutorialTalkStep(
            $"땅은 {Accent}바이옴{AccentEnd}으로 나뉘어 있습니다. 풀밭, 흙, 모래, 바위, 산처럼 지형이 다르게 보이는 구역이지요.",
            "바이옴마다 나타나는 자원이 다릅니다. 찾는 재료가 보이지 않으면 다른 바이옴까지 달려가 보세요."));
    }

    // B로 건물 선택 UI를 열고, 조준점으로 자리를 정해 대장간을 짓게 한다.
    private static void AddBuilding(List<TutorialStep> steps)
    {
        steps.Add(new TutorialTaskStep(
            new[] { "재료를 모았으면 건물을 지을 차례입니다." },
            runner => new TutorialStepStatus(
                $"{Accent}B{AccentEnd} 키를 눌러 건물 선택 창을 열어 보세요.",
                runner.Context.IsBuildingSelectOpen || runner.Context.IsPlacingBuilding || runner.Context.Lab != null)));

        steps.Add(new TutorialTaskStep(
            new[]
            {
                $"목록에서 {Accent}대장간{AccentEnd}을 고르고 [건축 시작]을 누르세요.",
                $"그러면 화면 가운데 조준점을 따라 건물 그림자가 움직입니다. 파란색일 때 {Accent}좌클릭{AccentEnd}이나 {Accent}F{AccentEnd}로 확정하면 그 자리에 건물이 섭니다.",
                "빨간색이면 그 자리에는 지을 수 없다는 뜻이에요. 우클릭이나 ESC로 취소할 수도 있습니다.",
            },
            runner => HandleBuildStatus(runner, ForgeBuildingName, runner.Context.Lab != null)));
    }

    // 대장간 앞에서 휠로 [작업대 열기]를 골라 F로 열고, 작업대로 좀돌날을 만들게 한다.
    private static void AddWorkbench(List<TutorialStep> steps)
    {
        steps.Add(new TutorialTaskStep(
            new[] { "대장간에는 재료를 다듬는 작업대가 있습니다." },
            runner => new TutorialStepStatus(
                $"{Accent}대장간{AccentEnd} 앞으로 걸어가 명령 목록을 띄워 보세요.",
                runner.Interaction.IsFacing<Lab>() || runner.Context.IsWorkbenchOpen)));

        steps.Add(new TutorialTaskStep(
            new[] { $"명령이 여러 개면 {Accent}마우스 휠{AccentEnd}로 고릅니다. 휠을 올리면 위 명령, 내리면 아래 명령이 골라져요." },
            runner => HandleOpenStatus<Lab>(runner, ForgeBuildingName, WorkbenchCommand, runner.Context.IsWorkbenchOpen)));

        steps.Add(new TutorialTaskStep(
            new[]
            {
                "작업대는 재료를 놓은 <b>모양</b>대로 물건을 만듭니다. 왼쪽 보유 목록의 재료를 끌어다 가운데 격자에 놓으세요.",
                "잘못 놓았다면 격자의 칸을 클릭해 다시 비울 수 있습니다.",
            },
            HandleMicrobladeStatus));
    }

    // 창고를 짓고 보관 중인 재료를 확인하게 한다.
    private static void AddWarehouse(List<TutorialStep> steps)
    {
        steps.Add(new TutorialTaskStep(
            new[] { "모은 재료는 창고에서 한눈에 볼 수 있습니다. 앞에서 배운 대로 창고를 지어 봅시다." },
            runner => HandleBuildStatus(runner, WarehouseBuildingName, runner.Context.Warehouse != null)));

        steps.Add(new TutorialTaskStep(
            null,
            runner => HandleOpenStatus<Warehouse>(runner, WarehouseBuildingName, WarehouseCommand, runner.Context.IsWarehouseOpen)));

        steps.Add(new TutorialTalkStep(
            $"창고 목록에서 방금 만든 {Accent}좀돌날{AccentEnd}이 있는지 확인해 보세요. 확인했다면 {Accent}Space{AccentEnd}를 눌러 주세요."));
    }

    // 근거지를 짓고 도감에서 조합법 힌트를 받아 보게 한다.
    private static void AddHomeBase(List<TutorialStep> steps)
    {
        steps.Add(new TutorialTaskStep(
            new[] { "마지막으로 근거지입니다. 근거지에서는 지금까지 알아낸 것들을 도감으로 볼 수 있습니다." },
            runner => HandleBuildStatus(runner, HomeBaseBuildingName, runner.Context.HomeBase != null)));

        steps.Add(new TutorialTaskStep(
            null,
            runner => HandleOpenStatus<HomeBase>(runner, HomeBaseBuildingName, CodexCommand, runner.Context.IsCodexOpen)));

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
            $"여기까지입니다. {Accent}고인돌{AccentEnd}을 지으면 연구로 새로운 기술도 열 수 있어요.",
            "이제 캐고, 만들고, 지으며 이 땅의 역사를 넓혀 보세요. 도움이 필요하면 근거지의 도감을 열어 보세요. 행운을 빕니다!"));
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
        string progress = $"({Mathf.Min(gained, StoneGoal)}/{StoneGoal})";
        bool isDone = gained >= StoneGoal;

        if (runner.Interaction.HasCommand(TutorialInteractionWatcher.GatherCommand))
            return new TutorialStepStatus($"{Accent}F{AccentEnd}를 눌러 {Accent}돌{AccentEnd}을 {StoneGoal}개 캐 보세요. {progress}", isDone);

        return new TutorialStepStatus($"{Accent}돌{AccentEnd} 앞으로 걸어가 {Accent}F{AccentEnd}로 캐 보세요. 하나를 다 캐면 다음 돌을 찾아가면 됩니다. {progress}", isDone);
    }

    // 건물을 짓는 진행도를 지금 어느 과정(선택 창 열기 → 건물 고르기 → 자리 정하기)에 있는지에 맞춰 문구와 판정으로 만든다.
    private static TutorialStepStatus HandleBuildStatus(TutorialRunner runner, string buildingName, bool isBuilt)
    {
        string stone = DescribeStone(runner);
        TutorialContext context = runner.Context;

        if (context.IsPlacingBuilding)
            return new TutorialStepStatus(
                $"조준점으로 자리를 정하고 {Accent}좌클릭{AccentEnd}이나 {Accent}F{AccentEnd}로 확정하세요. 빨간색이면 다른 자리를 찾아 보세요. {stone}",
                isBuilt);

        if (context.IsBuildingSelectOpen)
            return new TutorialStepStatus(
                $"{Accent}{buildingName}{AccentEnd}{ObjectParticle(buildingName)} 고르고 [건축 시작]을 누르세요. 재료가 모자라면 ESC로 창을 닫고 더 캐 오세요. {stone}",
                isBuilt);

        return new TutorialStepStatus(
            $"{Accent}B{AccentEnd} 키로 건물 선택 창을 열어 {Accent}{buildingName}{AccentEnd}{ObjectParticle(buildingName)} 지어 보세요. {stone}",
            isBuilt);
    }

    // 건물 앞에서 명령을 골라 여는 진행도를, 건물 앞에 섰는지와 지금 골라진 명령에 맞춰 문구와 판정으로 만든다.
    // 같은 목록에 [철거]가 함께 있으므로, 잘못 골랐을 때는 실행하지 말라고 짚어 준다.
    private static TutorialStepStatus HandleOpenStatus<T>(TutorialRunner runner, string buildingName, string commandName, bool isOpen) where T : Component
    {
        bool isDone = isOpen || runner.Interaction.WasExecuted(commandName);
        TutorialInteractionWatcher interaction = runner.Interaction;

        if (!interaction.IsFacing<T>())
            return new TutorialStepStatus($"{Accent}{buildingName}{AccentEnd} 앞으로 걸어가 명령 목록을 띄워 주세요.", isDone);

        if (interaction.SelectedName != commandName)
            return new TutorialStepStatus(
                $"{Accent}마우스 휠{AccentEnd}로 {Accent}[{commandName}]{AccentEnd}{ObjectParticle(commandName)} 골라 주세요. [{DemolishCommand}]을 실행하면 건물이 사라지니 조심하세요!",
                isDone);

        return new TutorialStepStatus($"{Accent}F{AccentEnd}를 눌러 {Accent}[{commandName}]{AccentEnd}{ObjectParticle(commandName)} 실행하세요.", isDone);
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
        string help = stone >= MicrobladeStoneCost ? string.Empty : " 돌이 모자라면 ESC로 작업대를 닫고 더 캐 온 뒤, 대장간에서 다시 여세요.";

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

    // 지금까지 움직인 거리와 목표 거리를 "(3/8m)" 꼴의 짧은 진행도 문구로 만든다.
    private static string DescribeDistance(float distance, float goal)
    {
        return $"({Mathf.FloorToInt(Mathf.Min(distance, goal))}/{Mathf.RoundToInt(goal)}m)";
    }

    // 낱말의 마지막 글자에 받침이 있으면 "을", 없으면 "를"을 돌려준다. (대장간을, 창고를, 작업대 열기를)
    private static string ObjectParticle(string word)
    {
        if (string.IsNullOrEmpty(word)) return "을";

        char last = word[word.Length - 1];
        if (last < '가' || last > '힣') return "을";

        return (last - '가') % 28 != 0 ? "을" : "를";
    }
}
