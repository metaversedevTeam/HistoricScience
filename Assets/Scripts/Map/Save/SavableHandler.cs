using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// ISavable 컴포넌트들이 컴포지션으로 사용하는 공용 저장 도우미 순수 C# 클래스. xz위치와 y축 각도, 같은 오브젝트에 붙은 IStatePart 부품들의 캡처/복원을 담당한다.
[Serializable]
public class SavableHandler
{
    // 위치/각도와 부품 상태들의 직렬화 래퍼
    [Serializable]
    private class SaveState
    {
        public float X;
        public float Z;
        public float YAngle;
        public List<PartState> Parts = new();
    }

    // 부품 하나의 타입 이름과 캡처된 상태 JSON 한 쌍. 컴포넌트 순서가 바뀌어도 짝을 찾을 수 있도록 타입 이름을 함께 저장한다.
    [Serializable]
    private struct PartState
    {
        public string TypeName;
        public string Json;
    }

    // 로드 시 저장 프리팹 목록에서 프리팹을 찾는 식별 키. 레지스트리 에셋의 항목과 일치해야 한다.
    [SerializeField] private string _prefabId;

    public string PrefabId => _prefabId;

    // 상태 JSON에서 xz위치만 읽어 반환한다. 스포너가 항목이 속한 청크를 판정할 때 사용한다. 읽지 못하면 false를 반환한다.
    public static bool TryReadPositionXZ(string json, out Vector2 positionXZ)
    {
        positionXZ = Vector2.zero;
        if (string.IsNullOrEmpty(json))
            return false;

        try
        {
            SaveState state = JsonUtility.FromJson<SaveState>(json);
            if (state == null)
                return false;

            positionXZ = new Vector2(state.X, state.Z);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    // 대상 트랜스폼의 현재 xz위치와 y축 각도, 붙어 있는 부품들의 상태를 JSON 문자열로 캡처한다.
    public string CaptureJson(Transform target)
    {
        SaveState state = new SaveState
        {
            X = target.position.x,
            Z = target.position.z,
            YAngle = target.eulerAngles.y,
            Parts = HandleCaptureParts(target),
        };

        return JsonUtility.ToJson(state);
    }

    // JSON 문자열의 xz위치와 y축 각도, 부품 상태를 대상에 복원한다. 높이는 지면 스냅으로 맞춘다.
    public void ApplyJson(Transform target, string json)
    {
        SaveState state = JsonUtility.FromJson<SaveState>(json);
        if (state == null) return;

        target.rotation = Quaternion.Euler(0f, state.YAngle, 0f);
        PlaceAt(target, new Vector3(state.X, target.position.y, state.Z));
        HandleApplyParts(target, state.Parts);
    }

    // 대상에 붙어 있는 IStatePart 부품들의 상태를 타입 이름과 함께 캡처한다.
    private List<PartState> HandleCaptureParts(Transform target)
    {
        List<PartState> parts = new();

        foreach (IStatePart part in target.GetComponents<IStatePart>())
        {
            parts.Add(new PartState
            {
                TypeName = part.GetType().Name,
                Json = part.CaptureJson(),
            });
        }

        return parts;
    }

    // 저장된 부품 상태를 타입 이름이 같은 부품에 복원한다. 같은 타입이 여러 개면 저장된 순서대로 짝지어 주고, 짝이 없는 부품은 경고를 남기고 건너뛴다.
    private void HandleApplyParts(Transform target, List<PartState> savedParts)
    {
        if (savedParts == null || savedParts.Count == 0) return;

        foreach (IStatePart part in target.GetComponents<IStatePart>())
        {
            string typeName = part.GetType().Name;
            int index = savedParts.FindIndex(saved => saved.TypeName == typeName);

            if (index < 0)
            {
                Debug.LogWarning($"SavableHandler: '{target.name}'의 {typeName} 상태가 저장 데이터에 없어 복원하지 않습니다.");
                continue;
            }

            part.ApplyJson(savedParts[index].Json);
            savedParts.RemoveAt(index);
        }
    }

    // 대상을 지정한 위치로 옮기고, 지면 스냅이 있으면 높이를 맞추고, 내브메시 에이전트가 있으면 워프로 내부 위치를 동기화한다.
    public static void PlaceAt(Transform target, Vector3 position)
    {
        target.position = position;

        if (target.TryGetComponent(out GroundSnapper snapper))
            snapper.SnapToGround();

        // 에이전트 내부 위치가 소환 시점 자리로 남아 있지 않도록 워프로 맞춘다. 주변에 내브메시가 아직 없어
        // 실패하더라도, GroundMover가 다음 이동 명령 때 주변 내브메시로 다시 워프하므로 문제없다.
        if (target.TryGetComponent(out NavMeshAgent agent) && agent.enabled)
            agent.Warp(target.position);
    }
}
