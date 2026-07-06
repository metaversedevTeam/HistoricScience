using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HistoricScience.Test
{
    // 보로노이 기반 터레인 칠하기를 보여주는 독립 실행형 테스트 씬을 생성하는 에디터 유틸리티
    public static class VoronoiTerrainTestSceneSetup
    {
        // 테스트 씬이 저장될 루트 폴더 경로. TerrainData는 에셋으로 저장하지 않고 씬에 인메모리로 포함된다.
        private const string k_TestRoot = "Assets/ExternalAssets/Test";
        // 생성될 테스트 씬 파일 경로
        private const string k_ScenePath = k_TestRoot + "/Scenes/VoronoiTerrainTest.unity";
        // 바다 평면에 사용할 머티리얼 경로
        private const string k_SeaMaterialPath = "Assets/ExternalAssets/Procedural Water Shader/Materials/Pool Water.mat";
        // 바다 평면의 해수면 높이(0~1 정규화, 터레인 최대 높이 기준). 바다 바이옴의 지형보다 높고 육지 바이옴보다 낮아야 바다 영역만 물에 잠긴다.
        private const float k_SeaLevelNormalizedHeight = 0.12f;
        // Terrain, TerrainCollider, MapDataGenerator, TerrainPainter가 미리 구성되어 있는 터레인 프리팹 경로
        private const string k_TerrainPrefabPath = "Assets/Prefabs/Environment/MapChunkTerrain.prefab";

        // MapChunkManager로 청크를 소환해 프리팹에 설정된 값 그대로 보로노이 지형을 굽고 테스트 씬을 저장한다.
        [MenuItem("HistoricScience/Test/Create Voronoi Terrain Test Scene")]
        public static void CreateScene()
        {
            HandleEnsureFolders();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            HandleCreateLight();

            MapChunkManager mapChunkManager = HandleCreateMapChunkManager();
            GameObject chunkObject = mapChunkManager.DrawChunk(Vector2Int.zero);
            chunkObject.name = "VoronoiTestTerrain";
            Terrain terrain = chunkObject.GetComponent<Terrain>();

            HandleCreateSea(terrain);

            EditorSceneManager.SaveScene(scene, k_ScenePath);
            Debug.Log($"Voronoi terrain test scene created at {k_ScenePath}");
        }

        // 테스트 씬을 저장할 폴더가 없으면 생성한다.
        private static void HandleEnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(k_TestRoot))
                AssetDatabase.CreateFolder("Assets/ExternalAssets", "Test");

            if (!AssetDatabase.IsValidFolder(k_TestRoot + "/Scenes"))
                AssetDatabase.CreateFolder(k_TestRoot, "Scenes");
        }

        // 칠해진 터레인이 잘 보이도록 씬에 디렉셔널 라이트를 추가한다.
        private static void HandleCreateLight()
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // Terrain, TerrainCollider, MapDataGenerator, TerrainPainter가 미리 구성된 프리팹을 청크 프리팹으로 갖는 MapChunkManager를 생성한다.
        // 이후 청크 소환/굽기는 모두 MapChunkManager를 통해 이뤄지며, TerrainPainter는 직접 참조하지 않는다.
        private static MapChunkManager HandleCreateMapChunkManager()
        {
            GameObject terrainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_TerrainPrefabPath);

            GameObject managerObject = new GameObject("MapChunkManager");
            MapChunkManager mapChunkManager = managerObject.AddComponent<MapChunkManager>();

            SerializedObject serializedManager = new SerializedObject(mapChunkManager);
            serializedManager.FindProperty("m_ChunkPrefab").objectReferenceValue = terrainPrefab;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();

            return mapChunkManager;
        }

        // 터레인 전체를 덮는 크기의 평면을 해수면 높이에 배치하고 Pool Water 머티리얼을 입혀 바다를 만든다.
        private static void HandleCreateSea(Terrain terrain)
        {
            TerrainData terrainData = terrain.terrainData;
            Material seaMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_SeaMaterialPath);

            GameObject seaObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
            seaObject.name = "Sea";
            Object.DestroyImmediate(seaObject.GetComponent<MeshCollider>());
            seaObject.GetComponent<MeshRenderer>().sharedMaterial = seaMaterial;

            // 기본 Plane 메시는 10x10 유닛이므로, 터레인의 가로/세로 크기에 맞추려면 10으로 나눈 배율로 스케일해야 한다.
            seaObject.transform.localScale = new Vector3(terrainData.size.x / 10f, 1f, terrainData.size.z / 10f);
            seaObject.transform.position = terrain.transform.position + new Vector3(
                terrainData.size.x * 0.5f,
                terrainData.size.y * k_SeaLevelNormalizedHeight,
                terrainData.size.z * 0.5f);
        }
    }
}
