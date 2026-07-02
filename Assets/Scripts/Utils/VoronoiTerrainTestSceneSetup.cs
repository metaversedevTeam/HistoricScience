using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HistoricScience.Test
{
    // 보로노이 기반 터레인 칠하기를 보여주는 독립 실행형 테스트 씬을 생성하는 에디터 유틸리티
    public static class VoronoiTerrainTestSceneSetup
    {
        // 테스트 산출물(씬, 터레인 데이터, 레이어, 바이옴 SO)이 저장될 루트 폴더 경로
        private const string k_TestRoot = "Assets/ExternalAssets/Test";
        // 생성될 테스트 씬 파일 경로
        private const string k_ScenePath = k_TestRoot + "/Scenes/VoronoiTerrainTest.unity";
        // 생성될 TerrainData 에셋 경로
        private const string k_TerrainDataPath = k_TestRoot + "/Terrain/VoronoiTestTerrainData.asset";
        // 생성될 TerrainLayer 에셋들이 저장될 폴더 경로
        private const string k_LayersFolder = k_TestRoot + "/TerrainLayers";
        // 생성될 MapBiome SO 에셋들이 저장될 폴더 경로
        private const string k_BiomesFolder = k_TestRoot + "/Biomes";
        // 바다 평면에 사용할 머티리얼 경로
        private const string k_SeaMaterialPath = "Assets/ExternalAssets/Procedural Water Shader/Materials/Pool Water.mat";
        // 바다 평면의 해수면 높이(0~1 정규화, 터레인 최대 높이 기준)
        private const float k_SeaLevelNormalizedHeight = -1f;
        // 정점 범위 밖일 때 대신 사용할 기본 바이옴의 이름
        private const string k_DefaultBiomeName = "SandUnderwater";

        // 터레인 레이어(바이옴)를 만들 때 텍스처를 가져올 원본 머티리얼 경로 목록
        private static readonly string[] k_SourceMaterialPaths =
        {
            "Assets/ExternalAssets/Cartoon_Texture_Pack/GRASS/GRASS_Dense/GRASS_Dense_Tint_01/Materials/Grass_Dense_Tint_01_Base_A.mat",
            "Assets/ExternalAssets/Cartoon_Texture_Pack/DIRT/Dirt_Path/Materials/Dirt_Path.mat",
            "Assets/ExternalAssets/Cartoon_Texture_Pack/SAND/SAND_Beach/Materials/Sand_Beach_Base.mat",
            "Assets/ExternalAssets/Cartoon_Texture_Pack/ROCKS/ROCKS_Cliff/Materials/Rocks_Cliff_A_BC_A.mat",
            "Assets/ExternalAssets/Cartoon_Texture_Pack/ROCKS/ROCKS_Volcanic/Materials/Rocks_Volcanic_A.mat",
            "Assets/ExternalAssets/Cartoon_Texture_Pack/SAND/SAND_Underwater/Materials/Sand_Underwater_Base.mat",
        };

        // 각 터레인 레이어에 대응하는 바이옴 정의. 순서가 k_SourceMaterialPaths와 일치해야 한다.
        private static readonly (string name, Color color)[] k_BiomeDefinitions =
        {
            ("Grass",          new Color(0.2f, 0.8f, 0.2f)),
            ("Dirt",           new Color(0.6f, 0.4f, 0.2f)),
            ("SandBeach",      new Color(0.9f, 0.85f, 0.5f)),
            ("RocksCliff",     new Color(0.5f, 0.5f, 0.5f)),
            ("RocksVolcanic",  new Color(0.3f, 0.1f, 0.1f)),
            ("SandUnderwater", new Color(0.3f, 0.6f, 0.8f)),
        };

        // 터레인을 만들고, 바이옴 SO와 터레인 레이어를 생성한 뒤, 보로노이로 칠하고 테스트 씬을 저장한다.
        [MenuItem("HistoricScience/Test/Create Voronoi Terrain Test Scene")]
        public static void CreateScene()
        {
            HandleEnsureFolders();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            HandleCreateLight();
            Terrain terrain = HandleCreateTerrain();
            TerrainLayer[] layers = HandleCreateTerrainLayers();
            MapBiome[] biomes = HandleCreateBiomes(layers);

            MapDataGenerator mapDataGenerator = terrain.gameObject.AddComponent<MapDataGenerator>();
            HandleConfigureMapDataGenerator(mapDataGenerator, biomes);

            TerrainPainter painter = terrain.gameObject.AddComponent<TerrainPainter>();
            HandleConfigurePainter(painter, mapDataGenerator, terrain);
            painter.PaintVoronoiTerrain();

            HandleCreateSea(terrain);

            EditorSceneManager.SaveScene(scene, k_ScenePath);
            Debug.Log($"Voronoi terrain test scene created at {k_ScenePath}");
        }

        // 테스트 씬, 터레인 데이터, 터레인 레이어, 바이옴 SO 에셋을 저장할 폴더가 없으면 생성한다.
        private static void HandleEnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(k_TestRoot))
                AssetDatabase.CreateFolder("Assets/ExternalAssets", "Test");

            if (!AssetDatabase.IsValidFolder(k_TestRoot + "/Scenes"))
                AssetDatabase.CreateFolder(k_TestRoot, "Scenes");

            if (!AssetDatabase.IsValidFolder(k_TestRoot + "/Terrain"))
                AssetDatabase.CreateFolder(k_TestRoot, "Terrain");

            if (!AssetDatabase.IsValidFolder(k_LayersFolder))
                AssetDatabase.CreateFolder(k_TestRoot, "TerrainLayers");

            if (!AssetDatabase.IsValidFolder(k_BiomesFolder))
                AssetDatabase.CreateFolder(k_TestRoot, "Biomes");
        }

        // 칠해진 터레인이 잘 보이도록 씬에 디렉셔널 라이트를 추가한다.
        private static void HandleCreateLight()
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // 새로 저장한 TerrainData 에셋을 기반으로 새 터레인 게임오브젝트를 생성한다.
        private static Terrain HandleCreateTerrain()
        {
            TerrainData terrainData = new TerrainData
            {
                heightmapResolution = 129,
                alphamapResolution = 512,
                size = new Vector3(500f, 100f, 500f),
            };

            AssetDatabase.CreateAsset(terrainData, k_TerrainDataPath);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = "VoronoiTestTerrain";

            return terrainObject.GetComponent<Terrain>();
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

        // 각 원본 머티리얼의 텍스처로 TerrainLayer 에셋을 만들어 테스트 폴더에 저장한다.
        private static TerrainLayer[] HandleCreateTerrainLayers()
        {
            TerrainLayer[] layers = new TerrainLayer[k_SourceMaterialPaths.Length];

            for (int i = 0; i < k_SourceMaterialPaths.Length; i++)
            {
                Material sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_SourceMaterialPaths[i]);

                TerrainLayer layer = new TerrainLayer
                {
                    diffuseTexture = sourceMaterial.GetTexture("_BaseMap") as Texture2D,
                    normalMapTexture = sourceMaterial.GetTexture("_BumpMap") as Texture2D,
                    tileSize = new Vector2(10f, 10f),
                };

                string layerPath = $"{k_LayersFolder}/{sourceMaterial.name}.terrainlayer";
                AssetDatabase.CreateAsset(layer, layerPath);
                layers[i] = layer;
            }

            return layers;
        }

        // 바이옴 정의와 터레인 레이어로 MapBiome SO 에셋을 생성해 테스트 폴더에 저장한다.
        private static MapBiome[] HandleCreateBiomes(TerrainLayer[] layers)
        {
            MapBiome[] biomes = new MapBiome[k_BiomeDefinitions.Length];

            for (int i = 0; i < k_BiomeDefinitions.Length; i++)
            {
                MapBiome biome = ScriptableObject.CreateInstance<MapBiome>();

                SerializedObject serializedBiome = new SerializedObject(biome);
                serializedBiome.FindProperty("m_BiomeName").stringValue = k_BiomeDefinitions[i].name;
                serializedBiome.FindProperty("m_TerrainLayer").objectReferenceValue = layers[i];
                serializedBiome.FindProperty("m_GizmoColor").colorValue = k_BiomeDefinitions[i].color;
                serializedBiome.ApplyModifiedPropertiesWithoutUndo();

                string biomePath = $"{k_BiomesFolder}/{k_BiomeDefinitions[i].name}.asset";
                AssetDatabase.CreateAsset(biome, biomePath);
                biomes[i] = biome;
            }

            return biomes;
        }

        // 맵 데이터 생성기 컴포넌트에 보로노이 생성 파라미터와 바이옴 SO 목록을 연결한다.
        private static void HandleConfigureMapDataGenerator(MapDataGenerator mapDataGenerator, MapBiome[] biomes)
        {
            SerializedObject serializedGenerator = new SerializedObject(mapDataGenerator);
            serializedGenerator.FindProperty("m_RegionCount").intValue = 30;
            serializedGenerator.FindProperty("m_UseRandomSeed").boolValue = true;

            SerializedProperty biomesProperty = serializedGenerator.FindProperty("m_Biomes");
            biomesProperty.arraySize = biomes.Length;
            for (int i = 0; i < biomes.Length; i++)
            {
                biomesProperty.GetArrayElementAtIndex(i).objectReferenceValue = biomes[i];
            }

            serializedGenerator.FindProperty("m_DefaultBiome").objectReferenceValue = HandleFindBiomeByName(biomes, k_DefaultBiomeName);

            serializedGenerator.ApplyModifiedPropertiesWithoutUndo();
        }

        // 이름으로 바이옴 목록에서 하나를 찾는다.
        private static MapBiome HandleFindBiomeByName(MapBiome[] biomes, string name)
        {
            foreach (MapBiome biome in biomes)
            {
                if (biome.Name == name)
                    return biome;
            }

            return null;
        }

        // 페인터 컴포넌트에 맵 데이터 생성기와 터레인을 연결한다.
        private static void HandleConfigurePainter(TerrainPainter painter, MapDataGenerator mapDataGenerator, Terrain terrain)
        {
            SerializedObject serializedPainter = new SerializedObject(painter);
            serializedPainter.FindProperty("m_MapDataGenerator").objectReferenceValue = mapDataGenerator;
            serializedPainter.FindProperty("m_Terrain").objectReferenceValue = terrain;

            serializedPainter.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
