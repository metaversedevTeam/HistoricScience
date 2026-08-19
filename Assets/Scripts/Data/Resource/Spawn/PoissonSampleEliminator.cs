using System.Collections.Generic;
using UnityEngine;

// 가중치 기반 샘플 소거(Yuksel 2015, Sample Elimination for Generating Poisson Disk Sample Sets) 알고리즘 구현.
// 넉넉하게 뽑아 둔 후보 점들 중 이웃과 가까이 뭉친 것부터 지워 나가, 목표 개수만큼의 고르게 퍼진 점 집합을 남긴다.
// 거리는 도메인의 마주 보는 변이 이어져 있다고 보고(주기 경계) 재므로, 가장자리 점도 안쪽 점과 이웃 수가 같아 가장자리에 몰리는 편향이 생기지 않는다.
// 소환 규칙과 무관한 순수 2D 점 계산만 하므로 Unity 씬 API에 의존하지 않는다.
public class PoissonSampleEliminator
{
    // 가중치 제한 계수(논문 권장값 β = 0.65). 거리 하한을 이 비율만큼 낮춰 잡는다.
    private const float k_Beta = 0.65f;
    // 가중치 제한 지수(논문 권장값 γ = 1.5). 남길 비율이 높을수록 거리 하한을 빠르게 0으로 줄인다.
    private const float k_Gamma = 1.5f;
    // 이웃 검사에 쓸 한 축당 격자 칸 수. 칸 한 변이 최대 거리 이상이라 이 범위 밖에는 이웃이 있을 수 없다.
    private const int k_NeighborCellCount = 3;

    // 소거 대상 후보 점들. 모두 (0, 0)부터 도메인 크기 사이에 있어야 한다.
    private readonly List<Vector2> m_Samples;
    // 이웃으로 볼 최대 거리. 이보다 멀리 떨어진 점끼리는 서로의 가중치에 영향을 주지 않는다.
    private readonly float m_MaxDistance;
    // 점들이 놓인 도메인의 크기. 이 크기만큼 떨어진 위치는 같은 자리로 보고 거리를 잰다.
    private readonly Vector2 m_DomainSize;

    // 이웃 검색용 균등 격자. 칸 한 변이 최대 거리 이상이 되도록 칸 수를 내림으로 잡아, 격자가 도메인을 빈틈없이 덮고 감싸기도 정확해진다.
    private readonly int m_GridWidth;
    private readonly int m_GridDepth;
    private readonly float m_CellWidth;
    private readonly float m_CellDepth;
    // 격자 칸별 점 목록을 담은 압축 배열. m_CellStarts[c]부터 m_CellStarts[c + 1] 직전까지가 c번 칸에 속한 점 인덱스다.
    private readonly int[] m_CellStarts;
    private readonly int[] m_CellSamples;
    // 각 점이 속한 격자 칸 번호. 이웃 검색 때마다 다시 계산하지 않으려고 미리 담아 둔다.
    private readonly int[] m_SampleCells;

    // 각 점의 현재 가중치. 이웃이 가깝고 많을수록 커지며, 가장 큰 점부터 제거된다.
    private float[] m_Weights;
    // 가중치 최대 힙. 원소는 점 인덱스이고, m_HeapPositions로 점에서 힙 위치를 역참조한다.
    private int[] m_Heap;
    private int[] m_HeapPositions;
    private int m_HeapCount;
    // 가중치를 계산할 때 거리를 이 값 아래로는 내려가지 않게 잘라, 거의 겹친 점들이 서로를 과대평가하는 것을 막는다.
    private float m_MinDistance;

    public PoissonSampleEliminator(List<Vector2> samples, float maxDistance, Vector2 domainSize)
    {
        m_Samples = samples;
        m_MaxDistance = maxDistance;
        m_DomainSize = domainSize;

        // 칸 수를 내림으로 잡으면 칸 한 변이 최대 거리보다 커지므로, 이웃은 항상 인접한 3×3 칸 안에서 모두 찾을 수 있다.
        m_GridWidth = IsUsable() ? Mathf.Max(1, Mathf.FloorToInt(domainSize.x / maxDistance)) : 1;
        m_GridDepth = IsUsable() ? Mathf.Max(1, Mathf.FloorToInt(domainSize.y / maxDistance)) : 1;
        m_CellWidth = domainSize.x / m_GridWidth;
        m_CellDepth = domainSize.y / m_GridDepth;

        HandleBuildGrid(out m_CellStarts, out m_CellSamples, out m_SampleCells);
    }

    // 후보 점들을 목표 개수만 남을 때까지 소거하고, 각 점이 살아남았는지를 입력 순서 그대로 담은 배열을 돌려준다.
    public bool[] Eliminate(int targetCount)
    {
        bool[] survived = new bool[m_Samples.Count];
        for (int i = 0; i < survived.Length; i++)
            survived[i] = true;

        // 남길 개수가 후보 수 이상이거나 이웃 판정 자체가 불가능하면 소거할 이유가 없다.
        if (targetCount >= m_Samples.Count || !IsUsable())
            return survived;

        m_MinDistance = HandleGetMinDistance(targetCount);
        HandleComputeWeights();
        HandleBuildHeap();

        while (m_HeapCount > targetCount)
        {
            int eliminated = HandlePopHeaviest();
            survived[eliminated] = false;
            HandleReduceNeighborWeights(eliminated, survived);
        }

        return survived;
    }

    // 이웃 판정에 필요한 값들이 모두 유효한지 확인한다.
    private bool IsUsable()
    {
        return m_MaxDistance > 0f && m_DomainSize.x > 0f && m_DomainSize.y > 0f;
    }

    // 남길 비율에 따라 가중치 계산에 쓸 거리 하한을 정한다. 많이 지워 낼수록 하한이 커져 남은 점들이 더 고르게 퍼진다.
    private float HandleGetMinDistance(int targetCount)
    {
        float ratio = (float)targetCount / m_Samples.Count;
        return m_MaxDistance * (1f - Mathf.Pow(ratio, k_Gamma)) * k_Beta;
    }

    // 칸마다 어떤 점이 들어 있는지 압축 배열로 정리해, 이웃 검색이 칸 단위로 끝나게 한다.
    private void HandleBuildGrid(out int[] cellStarts, out int[] cellSamples, out int[] sampleCells)
    {
        int sampleCount = m_Samples.Count;
        int cellCount = m_GridWidth * m_GridDepth;

        // 칸별 개수를 한 칸씩 밀어 세어 두면, 누적합만으로 각 칸이 시작하는 위치가 된다.
        cellStarts = new int[cellCount + 1];
        cellSamples = new int[sampleCount];
        sampleCells = new int[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            int cell = HandleGetCellIndex(m_Samples[i]);
            sampleCells[i] = cell;
            cellStarts[cell + 1]++;
        }

        for (int cell = 1; cell <= cellCount; cell++)
            cellStarts[cell] += cellStarts[cell - 1];

        int[] cursors = new int[cellCount];
        for (int cell = 0; cell < cellCount; cell++)
            cursors[cell] = cellStarts[cell];

        for (int i = 0; i < sampleCount; i++)
        {
            int cell = sampleCells[i];
            cellSamples[cursors[cell]] = i;
            cursors[cell]++;
        }
    }

    // 점 하나가 속한 격자 칸 번호를 구한다.
    private int HandleGetCellIndex(Vector2 sample)
    {
        int cellX = Mathf.Clamp((int)(sample.x / m_CellWidth), 0, m_GridWidth - 1);
        int cellZ = Mathf.Clamp((int)(sample.y / m_CellDepth), 0, m_GridDepth - 1);

        return cellZ * m_GridWidth + cellX;
    }

    // 모든 점의 초기 가중치를 이웃과의 거리로부터 계산한다.
    private void HandleComputeWeights()
    {
        m_Weights = new float[m_Samples.Count];

        for (int i = 0; i < m_Samples.Count; i++)
        {
            float weight = 0f;
            HandleGetNeighborCellRange(m_SampleCells[i], out int firstCellX, out int countX, out int firstCellZ, out int countZ);

            for (int offsetZ = 0; offsetZ < countZ; offsetZ++)
            {
                for (int offsetX = 0; offsetX < countX; offsetX++)
                {
                    int cell = HandleGetNeighborCell(firstCellX + offsetX, firstCellZ + offsetZ);
                    for (int slot = m_CellStarts[cell]; slot < m_CellStarts[cell + 1]; slot++)
                    {
                        int neighbor = m_CellSamples[slot];
                        if (neighbor == i)
                            continue;

                        weight += HandleGetWeight(HandleGetDistance(m_Samples[i], m_Samples[neighbor]));
                    }
                }
            }

            m_Weights[i] = weight;
        }
    }

    // 제거된 점이 이웃들에게 주던 가중치를 되돌려, 아직 남아 있는 이웃들의 우선순위를 갱신한다.
    private void HandleReduceNeighborWeights(int eliminated, bool[] survived)
    {
        HandleGetNeighborCellRange(m_SampleCells[eliminated], out int firstCellX, out int countX, out int firstCellZ, out int countZ);

        for (int offsetZ = 0; offsetZ < countZ; offsetZ++)
        {
            for (int offsetX = 0; offsetX < countX; offsetX++)
            {
                int cell = HandleGetNeighborCell(firstCellX + offsetX, firstCellZ + offsetZ);
                for (int slot = m_CellStarts[cell]; slot < m_CellStarts[cell + 1]; slot++)
                {
                    int neighbor = m_CellSamples[slot];
                    if (neighbor == eliminated || !survived[neighbor])
                        continue;

                    float weight = HandleGetWeight(HandleGetDistance(m_Samples[eliminated], m_Samples[neighbor]));
                    if (weight <= 0f)
                        continue;

                    m_Weights[neighbor] -= weight;
                    // 가중치는 줄어들기만 하므로, 최대 힙에서는 아래로 내리는 것만으로 순서가 복구된다.
                    HandleSiftDown(m_HeapPositions[neighbor]);
                }
            }
        }
    }

    // 격자 칸 번호로부터 이웃 검사에 훑을 칸 범위를 구한다. 한 축의 칸 수가 3 미만이면 감싸기 때문에 같은 칸을 두 번 밟으므로, 그 축은 전체 칸을 한 번씩만 돈다.
    private void HandleGetNeighborCellRange(int cell, out int firstCellX, out int countX, out int firstCellZ, out int countZ)
    {
        int cellX = cell % m_GridWidth;
        int cellZ = cell / m_GridWidth;

        countX = Mathf.Min(k_NeighborCellCount, m_GridWidth);
        countZ = Mathf.Min(k_NeighborCellCount, m_GridDepth);
        firstCellX = m_GridWidth >= k_NeighborCellCount ? cellX - 1 : 0;
        firstCellZ = m_GridDepth >= k_NeighborCellCount ? cellZ - 1 : 0;
    }

    // 격자 밖으로 벗어난 칸 좌표를 반대쪽으로 감아 실제 칸 번호로 바꾼다.
    private int HandleGetNeighborCell(int cellX, int cellZ)
    {
        int wrappedX = (cellX % m_GridWidth + m_GridWidth) % m_GridWidth;
        int wrappedZ = (cellZ % m_GridDepth + m_GridDepth) % m_GridDepth;

        return wrappedZ * m_GridWidth + wrappedX;
    }

    // 두 점 사이 거리를 잰다. 도메인의 마주 보는 변이 이어져 있다고 보고, 도메인을 가로지르는 쪽과 감아 도는 쪽 중 가까운 쪽을 쓴다.
    private float HandleGetDistance(Vector2 left, Vector2 right)
    {
        float deltaX = Mathf.Abs(left.x - right.x);
        float deltaZ = Mathf.Abs(left.y - right.y);

        if (deltaX > m_DomainSize.x * 0.5f)
            deltaX = m_DomainSize.x - deltaX;
        if (deltaZ > m_DomainSize.y * 0.5f)
            deltaZ = m_DomainSize.y - deltaZ;

        return Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
    }

    // 두 점 사이 거리로부터 가중치 (1 - d / 최대 거리)^α 를 구한다(논문 권장값 α = 8). 최대 거리 밖이면 0이고, 하한보다 가까우면 하한 거리로 취급한다.
    private float HandleGetWeight(float distance)
    {
        if (distance >= m_MaxDistance)
            return 0f;

        float clamped = Mathf.Max(distance, m_MinDistance);
        float normalized = 1f - clamped / m_MaxDistance;
        // α가 8이라, 제곱을 세 번 반복하면 Mathf.Pow 없이 같은 값을 얻는다.
        float squared = normalized * normalized;
        float fourth = squared * squared;

        return fourth * fourth;
    }

    // 모든 점을 담은 가중치 최대 힙을 만든다.
    private void HandleBuildHeap()
    {
        m_HeapCount = m_Samples.Count;
        m_Heap = new int[m_HeapCount];
        m_HeapPositions = new int[m_HeapCount];

        for (int i = 0; i < m_HeapCount; i++)
        {
            m_Heap[i] = i;
            m_HeapPositions[i] = i;
        }

        for (int position = m_HeapCount / 2 - 1; position >= 0; position--)
            HandleSiftDown(position);
    }

    // 가중치가 가장 큰(= 이웃과 가장 뭉쳐 있는) 점을 힙에서 빼내 그 인덱스를 돌려준다.
    private int HandlePopHeaviest()
    {
        int heaviest = m_Heap[0];

        m_HeapCount--;
        if (m_HeapCount > 0)
        {
            m_Heap[0] = m_Heap[m_HeapCount];
            m_HeapPositions[m_Heap[0]] = 0;
            HandleSiftDown(0);
        }

        return heaviest;
    }

    // 힙의 한 위치에 있는 원소를 자식 중 더 무거운 쪽과 바꿔 가며 제자리를 찾아 준다.
    private void HandleSiftDown(int position)
    {
        while (true)
        {
            int left = position * 2 + 1;
            int right = left + 1;
            int heaviest = position;

            if (left < m_HeapCount && m_Weights[m_Heap[left]] > m_Weights[m_Heap[heaviest]])
                heaviest = left;
            if (right < m_HeapCount && m_Weights[m_Heap[right]] > m_Weights[m_Heap[heaviest]])
                heaviest = right;

            if (heaviest == position)
                return;

            HandleSwap(position, heaviest);
            position = heaviest;
        }
    }

    // 힙의 두 위치를 맞바꾸고 역참조 표도 함께 갱신한다.
    private void HandleSwap(int left, int right)
    {
        int leftSample = m_Heap[left];
        int rightSample = m_Heap[right];

        m_Heap[left] = rightSample;
        m_Heap[right] = leftSample;
        m_HeapPositions[rightSample] = left;
        m_HeapPositions[leftSample] = right;
    }
}
