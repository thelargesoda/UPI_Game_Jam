using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class PlantingScript : MonoBehaviour
{
    public const string FarmSavePrefsKey = "PlantingScript_FarmWorldSave_v1";

    public enum ToolMode
    {
        Tanam,
        Sekop,
        Siram
    }

    public enum TreeOption
    {
        Tree1 = 1,
        Tree2 = 2,
        Tree3 = 3
    }

    [Serializable]
    public class TreePlantedEvent : UnityEvent<TreeOption, int> { }

    [Serializable]
    public struct PlantedTreeRecord
    {
        public int x;
        public int y;
        public int z;
        public TreeOption treeType;
    }

    [Serializable]
    public struct ClearedCellRecord
    {
        public int x;
        public int y;
        public int z;
    }

    [Serializable]
    public struct ClearedPrefabRecord
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    private class FarmWorldSaveData
    {
        public List<PlantedTreeRecord> plantedTrees = new List<PlantedTreeRecord>();
        public List<ClearedCellRecord> clearedCells = new List<ClearedCellRecord>();
        public List<ClearedPrefabRecord> clearedPrefabs = new List<ClearedPrefabRecord>();
    }

    [Header("Tilemap & Tree Prefabs")]
    public Tilemap plantingTilemap;
    public Tilemap groundTilemap;         // Protected: will NOT be cleared or treated as obstacle
    public Tilemap ground2Tilemap;        // Protected: will NOT be cleared or treated as obstacle
    public Tilemap[] blockingTilemaps;    // Specific tilemaps that make a cell red/blocked when planting
    public bool includePlantingTilemapAsBlocked = false;
    public Tilemap[] clearableTilemaps;   // Single tile cleared at the planted cell (except ground & ground2)
    public Transform treeParent;          // Optional empty GameObject to keep spawned trees tidy
    public Vector3 spawnOffset = Vector3.zero;
    public GameObject tree1Prefab;
    public GameObject tree2Prefab;
    public GameObject tree3Prefab;

    [Header("Cross-Scene Persistence (Planted Trees, Cleared Tiles & Prefabs)")]
    [Tooltip("Saves planted trees, cleared tiles, and deleted prefabs to PlayerPrefs so new scenes inherit them")]
    public bool persistAcrossScenes = true;

    [Tooltip("Applies saved state automatically when this scene starts")]
    public bool applySavedStateOnStart = true;

    [Tooltip("Radius used to match/destroy saved cleared prefabs when entering a new scene")]
    public float savedPrefabMatchRadius = 0.5f;

    [Header("Tool Settings (Sekop / Tanam / Siram)")]
    public ToolMode currentTool = ToolMode.Tanam;
    public TreeOption selectedTree = TreeOption.Tree1;
    public Vector3Int sekopCellOffset = new Vector3Int(-3, -1, 0);
    public LayerMask sekopClearLayers;    // Optional Unity LayerMask for prefabs deleted with Sekop
    public float sekopPrefabClearRadius = 0.45f;
    public bool autoSwitchToTanamAfterPick = true;
    public bool closePanelAfterPick = false;

    [Header("Sekop Sorting Layer Filter")]
    public bool filterPrefabsBySortingLayer = true;
    public string[] sekopClearSortingLayers = new string[] { "Default" };

    [Header("Seed / Pupuk Selection Highlight")]
    public bool highlightSelectedSeedButton = true;
    public Color selectedSeedHighlightColor = new Color(1f, 0.25f, 0.25f, 0.45f);
    public Button tree1Button;
    public Button tree2Button;
    public Button tree3Button;

    [Header("Water (Siram) Visual")]
    public Sprite waterSprite; // Optional custom water sprite; falls back to tileHighlight's sprite
    public Color wateredColor = new Color(0.2f, 0.55f, 1f, 0.45f);
    public float waterDuration = 1f;
    public int waterSortingOrderOffset = 1;

    [Header("Cursor Highlight")]
    public Camera mainCamera;
    public GameObject tileHighlight; // World-space square sprite that snaps to hovered tile
    public Color validTileColor = new Color(0.3f, 1f, 0.3f, 0.45f);
    public Color occupiedTileColor = new Color(1f, 0.3f, 0.3f, 0.45f);
    public Color sekopTileColor = new Color(1f, 0.75f, 0.2f, 0.45f);

    [Header("UI Buttons, Hunger & Panels (Auto-Found if Left Empty)")]
    public GameObject plantPanel; // Panel with the 3 tree choice buttons
    public Button tanamButton;
    public Button siramButton;
    public Button pupukButton;
    public Button sekopButton;
    public HungerSystem hungerSystem;
    public string playerTag = "Player";

    [Header("Planted Tree Scores & Quest IDs")]
    public int tree1Score;
    public int tree2Score;
    public int tree3Score;
    public TreePlantedEvent onTreePlanted;

    [Tooltip("Quest ID incremented when planting Tree1 (leave blank to skip)")]
    public string tree1QuestId = "plant_tree1";

    [Tooltip("Quest ID incremented when planting Tree2 (leave blank to skip)")]
    public string tree2QuestId = "plant_tree2";

    [Tooltip("Quest ID incremented when planting Tree3 (leave blank to skip)")]
    public string tree3QuestId = "plant_tree3";

    [Tooltip("Optional Quest ID incremented whenever ANY tree is planted (leave blank to skip)")]
    public string anyTreePlantedQuestId = "";

    [Tooltip("Quest ID incremented when using Sekop (leave blank to skip)")]
    public string sekopQuestId = "Sekop_Clean";

    private static FarmWorldSaveData cachedSaveData;

    private readonly HashSet<Vector3Int> plantedCells = new HashSet<Vector3Int>();
    private readonly Dictionary<Vector3Int, GameObject> spawnedTreesByCell = new Dictionary<Vector3Int, GameObject>();

    private readonly Dictionary<Button, Graphic> seedGraphics = new Dictionary<Button, Graphic>();
    private readonly Dictionary<Graphic, Color> originalGraphicColors = new Dictionary<Graphic, Color>();

    private SpriteRenderer highlightRenderer;
    private Sprite defaultHighlightSprite;
    private Vector3Int hoveredCell;
    private bool isHoveringValidTile;
    private bool playerInArea;
    private bool isPanelOpen;

    private void Awake()
    {
        if (plantingTilemap == null)
        {
            plantingTilemap = GetComponent<Tilemap>();
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        AutoWireUI();
        CacheSeedButtonGraphics();
        RefreshSeedButtonHighlights();

        if (tileHighlight != null)
        {
            highlightRenderer = tileHighlight.GetComponent<SpriteRenderer>();
            if (highlightRenderer != null)
            {
                defaultHighlightSprite = highlightRenderer.sprite;
            }
            tileHighlight.SetActive(false);
        }

        if (plantPanel != null)
        {
            plantPanel.SetActive(false);
        }
    }

    private void Start()
    {
        if (persistAcrossScenes && applySavedStateOnStart)
        {
            ApplySavedFarmStateToScene();
        }
    }

    public static void ClearSavedFarmData()
    {
        cachedSaveData = new FarmWorldSaveData();
        PlayerPrefs.DeleteKey(FarmSavePrefsKey);
        PlayerPrefs.Save();
    }

    private static FarmWorldSaveData GetOrLoadSaveData()
    {
        if (cachedSaveData != null)
            return cachedSaveData;

        if (PlayerPrefs.HasKey(FarmSavePrefsKey))
        {
            string json = PlayerPrefs.GetString(FarmSavePrefsKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    cachedSaveData = JsonUtility.FromJson<FarmWorldSaveData>(json);
                }
                catch
                {
                    cachedSaveData = new FarmWorldSaveData();
                }
            }
        }

        if (cachedSaveData == null)
        {
            cachedSaveData = new FarmWorldSaveData();
        }

        return cachedSaveData;
    }

    private static void SaveFarmDataToPrefs()
    {
        if (cachedSaveData == null) return;
        string json = JsonUtility.ToJson(cachedSaveData);
        PlayerPrefs.SetString(FarmSavePrefsKey, json);
        PlayerPrefs.Save();
    }

    private void RecordClearedCell(Vector3Int cell)
    {
        if (!persistAcrossScenes) return;

        FarmWorldSaveData data = GetOrLoadSaveData();
        for (int i = 0; i < data.clearedCells.Count; i++)
        {
            ClearedCellRecord r = data.clearedCells[i];
            if (r.x == cell.x && r.y == cell.y && r.z == cell.z)
                return;
        }

        data.clearedCells.Add(new ClearedCellRecord { x = cell.x, y = cell.y, z = cell.z });
        SaveFarmDataToPrefs();
    }

    private void UnrecordClearedCell(Vector3Int cell)
    {
        if (!persistAcrossScenes) return;

        FarmWorldSaveData data = GetOrLoadSaveData();
        for (int i = data.clearedCells.Count - 1; i >= 0; i--)
        {
            ClearedCellRecord r = data.clearedCells[i];
            if (r.x == cell.x && r.y == cell.y && r.z == cell.z)
            {
                data.clearedCells.RemoveAt(i);
            }
        }
        SaveFarmDataToPrefs();
    }

    private void RecordPlantedTree(Vector3Int cell, TreeOption option)
    {
        if (!persistAcrossScenes) return;

        FarmWorldSaveData data = GetOrLoadSaveData();
        for (int i = data.plantedTrees.Count - 1; i >= 0; i--)
        {
            PlantedTreeRecord r = data.plantedTrees[i];
            if (r.x == cell.x && r.y == cell.y && r.z == cell.z)
            {
                data.plantedTrees.RemoveAt(i);
            }
        }

        data.plantedTrees.Add(new PlantedTreeRecord
        {
            x = cell.x,
            y = cell.y,
            z = cell.z,
            treeType = option
        });
        SaveFarmDataToPrefs();
    }

    private void UnrecordPlantedTree(Vector3Int cell)
    {
        if (!persistAcrossScenes) return;

        FarmWorldSaveData data = GetOrLoadSaveData();
        for (int i = data.plantedTrees.Count - 1; i >= 0; i--)
        {
            PlantedTreeRecord r = data.plantedTrees[i];
            if (r.x == cell.x && r.y == cell.y && r.z == cell.z)
            {
                data.plantedTrees.RemoveAt(i);
            }
        }
        SaveFarmDataToPrefs();
    }

    private void RecordClearedPrefab(Vector3 worldPos)
    {
        if (!persistAcrossScenes) return;

        FarmWorldSaveData data = GetOrLoadSaveData();
        data.clearedPrefabs.Add(new ClearedPrefabRecord
        {
            x = worldPos.x,
            y = worldPos.y,
            z = worldPos.z
        });
        SaveFarmDataToPrefs();
    }

    public void ApplySavedFarmStateToScene()
    {
        FarmWorldSaveData data = GetOrLoadSaveData();
        if (data == null) return;

        // 1. Re-apply cleared tiles (excluding protected ground)
        if (clearableTilemaps != null && data.clearedCells.Count > 0)
        {
            foreach (ClearedCellRecord r in data.clearedCells)
            {
                Vector3Int cell = new Vector3Int(r.x, r.y, r.z);
                foreach (Tilemap tilemap in clearableTilemaps)
                {
                    if (IsProtectedGround(tilemap)) continue;
                    tilemap.SetTile(cell, null);
                }
            }
        }

        // 2. Re-destroy matching prefabs that were deleted by Sekop
        if (data.clearedPrefabs.Count > 0)
        {
            HashSet<GameObject> matchedToDestroy = new HashSet<GameObject>();
            foreach (ClearedPrefabRecord p in data.clearedPrefabs)
            {
                Vector3 pos = new Vector3(p.x, p.y, p.z);
                Collider2D[] hits2D = Physics2D.OverlapCircleAll(pos, savedPrefabMatchRadius);
                foreach (Collider2D col in hits2D)
                {
                    if (col == null) continue;
                    GameObject candidate = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
                    if (candidate != null && candidate != gameObject && MatchesFilterForPrefabDestroy(candidate))
                    {
                        matchedToDestroy.Add(candidate);
                    }
                }
            }

            foreach (GameObject obj in matchedToDestroy)
            {
                if (obj != null) Destroy(obj);
            }
        }

        // 3. Re-spawn planted trees in the new scene
        if (plantingTilemap != null && data.plantedTrees.Count > 0)
        {
            foreach (PlantedTreeRecord treeRec in data.plantedTrees)
            {
                Vector3Int cell = new Vector3Int(treeRec.x, treeRec.y, treeRec.z);
                if (spawnedTreesByCell.ContainsKey(cell)) continue;

                GameObject prefab = GetTreePrefab(treeRec.treeType);
                if (prefab == null) continue;

                Vector3 spawnPos = plantingTilemap.GetCellCenterWorld(cell) + spawnOffset;
                GameObject spawnedTree = Instantiate(prefab, spawnPos, Quaternion.identity, treeParent);

                plantedCells.Add(cell);
                spawnedTreesByCell[cell] = spawnedTree;
            }
        }
    }

    private bool MatchesFilterForPrefabDestroy(GameObject candidate)
    {
        bool useLayerMask = sekopClearLayers.value != 0;
        bool useSortingLayer = filterPrefabsBySortingLayer && sekopClearSortingLayers != null && sekopClearSortingLayers.Length > 0;

        if (useLayerMask && ((sekopClearLayers.value & (1 << candidate.layer)) == 0))
            return false;

        if (useSortingLayer && !MatchesSortingLayer(candidate, true))
            return false;

        return true;
    }

    private void AutoWireUI()
    {
        if (hungerSystem == null)
        {
            hungerSystem = HungerSystem.Instance != null ? HungerSystem.Instance : FindFirstObjectByType<HungerSystem>();
        }

        if (plantPanel == null)
        {
            GameObject foundPanel = FindSceneObjectByName("PlantPanel");
            if (foundPanel != null) plantPanel = foundPanel;
        }

        if (tanamButton == null) tanamButton = FindButtonByName("Tanam");
        if (siramButton == null) siramButton = FindButtonByName("Siram");
        if (pupukButton == null) pupukButton = FindButtonByName("Pupuk");
        if (sekopButton == null) sekopButton = FindButtonByName("Sekop");

        if (tanamButton != null)
        {
            tanamButton.onClick.RemoveListener(SelectTanamMode);
            tanamButton.onClick.AddListener(SelectTanamMode);
        }

        if (siramButton != null)
        {
            siramButton.onClick.RemoveListener(SelectSiramMode);
            siramButton.onClick.AddListener(SelectSiramMode);
        }

        if (pupukButton != null)
        {
            pupukButton.onClick.RemoveListener(TogglePupukPanel);
            pupukButton.onClick.AddListener(TogglePupukPanel);
        }

        if (sekopButton != null)
        {
            sekopButton.onClick.RemoveListener(SelectSekopMode);
            sekopButton.onClick.AddListener(SelectSekopMode);
        }

        if (plantPanel != null)
        {
            if (tree1Button == null) tree1Button = FindChildButton(plantPanel.transform, "1");
            if (tree2Button == null) tree2Button = FindChildButton(plantPanel.transform, "1 (1)");
            if (tree3Button == null) tree3Button = FindChildButton(plantPanel.transform, "1 (2)");
        }

        if (tree1Button != null)
        {
            tree1Button.onClick.RemoveListener(SelectTree1);
            tree1Button.onClick.AddListener(SelectTree1);
        }

        if (tree2Button != null)
        {
            tree2Button.onClick.RemoveListener(SelectTree2);
            tree2Button.onClick.AddListener(SelectTree2);
        }

        if (tree3Button != null)
        {
            tree3Button.onClick.RemoveListener(SelectTree3);
            tree3Button.onClick.AddListener(SelectTree3);
        }
    }

    private void CacheSeedButtonGraphics()
    {
        RegisterSeedButton(tree1Button);
        RegisterSeedButton(tree2Button);
        RegisterSeedButton(tree3Button);
    }

    private void RegisterSeedButton(Button btn)
    {
        if (btn == null) return;

        Graphic target = btn.targetGraphic != null ? btn.targetGraphic : btn.GetComponent<Graphic>();
        if (target != null)
        {
            seedGraphics[btn] = target;
            if (!originalGraphicColors.ContainsKey(target))
            {
                originalGraphicColors[target] = target.color;
            }
        }
    }

    private void RefreshSeedButtonHighlights()
    {
        Button activeBtn = GetButtonForOption(selectedTree);

        foreach (KeyValuePair<Button, Graphic> entry in seedGraphics)
        {
            Button btn = entry.Key;
            Graphic graphic = entry.Value;
            if (btn == null || graphic == null) continue;

            if (!originalGraphicColors.TryGetValue(graphic, out Color baseColor))
            {
                baseColor = graphic.color;
                originalGraphicColors[graphic] = baseColor;
            }

            if (highlightSelectedSeedButton && btn == activeBtn)
            {
                graphic.color = Color.Lerp(baseColor, selectedSeedHighlightColor, selectedSeedHighlightColor.a);
            }
            else
            {
                graphic.color = baseColor;
            }
        }
    }

    private Button GetButtonForOption(TreeOption option)
    {
        switch (option)
        {
            case TreeOption.Tree1: return tree1Button;
            case TreeOption.Tree2: return tree2Button;
            case TreeOption.Tree3: return tree3Button;
            default: return tree1Button;
        }
    }

    private Button FindChildButton(Transform parent, string childName)
    {
        if (parent == null) return null;
        Transform found = parent.Find(childName);
        return found != null ? found.GetComponent<Button>() : null;
    }

    private Button FindButtonByName(string objName)
    {
        GameObject obj = FindSceneObjectByName(objName);
        return obj != null ? obj.GetComponent<Button>() : null;
    }

    private GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return null;

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj.scene.IsValid() && obj.name == objectName)
                return obj;
        }
        return null;
    }

    private HungerSystem GetHungerSystem()
    {
        if (hungerSystem != null) return hungerSystem;
        if (HungerSystem.Instance != null)
        {
            hungerSystem = HungerSystem.Instance;
            return hungerSystem;
        }
        hungerSystem = FindFirstObjectByType<HungerSystem>();
        return hungerSystem;
    }

    private void Update()
    {
        if (!playerInArea)
        {
            if (tileHighlight != null && tileHighlight.activeSelf)
                tileHighlight.SetActive(false);
            return;
        }

        UpdateCursorHighlight();

        if (Input.GetKeyDown(KeyCode.E))
        {
            TogglePupukPanel();
        }

        if (Input.GetMouseButtonDown(0) && isHoveringValidTile)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            switch (currentTool)
            {
                case ToolMode.Tanam:
                    TryPlantSelectedTree(hoveredCell);
                    break;

                case ToolMode.Sekop:
                    UseSekopAt(hoveredCell + sekopCellOffset);
                    break;

                case ToolMode.Siram:
                    UseSiramAt(hoveredCell);
                    break;
            }
        }
    }

    private bool IsProtectedGround(Tilemap tilemap)
    {
        return tilemap == null || tilemap == groundTilemap || tilemap == ground2Tilemap;
    }

    private bool IsCellOccupiedForPlanting(Vector3Int cell)
    {
        if (plantedCells.Contains(cell))
            return true;

        if (includePlantingTilemapAsBlocked && plantingTilemap != null && !IsProtectedGround(plantingTilemap))
        {
            if (plantingTilemap.HasTile(cell))
                return true;
        }

        Tilemap[] mapsToCheck = (blockingTilemaps != null && blockingTilemaps.Length > 0)
            ? blockingTilemaps
            : clearableTilemaps;

        if (mapsToCheck != null)
        {
            foreach (Tilemap map in mapsToCheck)
            {
                if (IsProtectedGround(map)) continue;
                if (!includePlantingTilemapAsBlocked && map == plantingTilemap) continue;

                if (map.HasTile(cell))
                    return true;
            }
        }

        return false;
    }

    private void UpdateCursorHighlight()
    {
        if (mainCamera == null || plantingTilemap == null || tileHighlight == null)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            tileHighlight.SetActive(false);
            isHoveringValidTile = false;
            return;
        }

        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = Mathf.Abs(mainCamera.transform.position.z - plantingTilemap.transform.position.z);

        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);
        Vector3Int cellPos = plantingTilemap.WorldToCell(mouseWorldPos);

        bool cellExists = plantingTilemap.HasTile(cellPos);

        if (cellExists || currentTool == ToolMode.Sekop)
        {
            hoveredCell = cellPos;
            isHoveringValidTile = cellExists;

            Vector3Int targetVisualCell = currentTool == ToolMode.Sekop
                ? cellPos + sekopCellOffset
                : cellPos;

            tileHighlight.SetActive(true);
            tileHighlight.transform.position = plantingTilemap.GetCellCenterWorld(targetVisualCell);

            if (highlightRenderer != null)
            {
                if (highlightRenderer.sprite != defaultHighlightSprite && defaultHighlightSprite != null)
                {
                    highlightRenderer.sprite = defaultHighlightSprite;
                }

                if (currentTool == ToolMode.Sekop)
                {
                    highlightRenderer.color = sekopTileColor;
                }
                else if (currentTool == ToolMode.Siram)
                {
                    highlightRenderer.color = wateredColor;
                }
                else
                {
                    highlightRenderer.color = IsCellOccupiedForPlanting(cellPos)
                        ? occupiedTileColor
                        : validTileColor;
                }
            }
        }
        else
        {
            isHoveringValidTile = false;
            tileHighlight.SetActive(false);
        }
    }

    // --- Toolbar / Mode Buttons ---

    public void SelectTanamMode()
    {
        currentTool = ToolMode.Tanam;
    }

    public void SelectSekopMode()
    {
        currentTool = ToolMode.Sekop;
        ClosePlantPanel();
    }

    public void SelectSiramMode()
    {
        currentTool = ToolMode.Siram;
        ClosePlantPanel();
    }

    public void TogglePupukPanel()
    {
        if (isPanelOpen)
            ClosePlantPanel();
        else
            OpenPlantPanel();
    }

    public void OpenPlantPanel()
    {
        if (plantPanel == null) return;
        isPanelOpen = true;
        plantPanel.SetActive(true);
        RefreshSeedButtonHighlights();
    }

    public void ClosePlantPanel()
    {
        isPanelOpen = false;
        if (plantPanel != null)
        {
            plantPanel.SetActive(false);
        }
    }

    // --- Pupuk Tree Choice Buttons ---

    public void SelectTree1()
    {
        ChooseTree(TreeOption.Tree1);
    }

    public void SelectTree2()
    {
        ChooseTree(TreeOption.Tree2);
    }

    public void SelectTree3()
    {
        ChooseTree(TreeOption.Tree3);
    }

    private void ChooseTree(TreeOption option)
    {
        selectedTree = option;
        RefreshSeedButtonHighlights();
        Debug.Log($"[PlantingScript] Selected Seed/Tree: {selectedTree}");

        if (autoSwitchToTanamAfterPick)
        {
            currentTool = ToolMode.Tanam;
        }

        if (closePanelAfterPick)
        {
            ClosePlantPanel();
        }
    }

    // --- Tool Actions ---

    private void TryPlantSelectedTree(Vector3Int cell)
    {
        if (!playerInArea || plantingTilemap == null)
            return;

        if (!plantingTilemap.HasTile(cell) || IsCellOccupiedForPlanting(cell))
            return;

        GameObject prefabToSpawn = GetTreePrefab(selectedTree);
        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"[PlantingScript] Prefab for {selectedTree} is not assigned!");
            return;
        }

        Vector3 spawnPos = plantingTilemap.GetCellCenterWorld(cell) + spawnOffset;
        GameObject spawnedTree = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity, treeParent);

        plantedCells.Add(cell);
        spawnedTreesByCell[cell] = spawnedTree;
        RecordPlantedTree(cell, selectedTree);

        ClearSingleTileAtCell(cell);
        AddTreeScore(selectedTree);
        NotifyQuestForPlantedTree(selectedTree);

        HungerSystem hs = GetHungerSystem();
        if (hs != null)
        {
            hs.ReduceHungerForPlant();
        }
    }

    private GameObject GetTreePrefab(TreeOption option)
    {
        switch (option)
        {
            case TreeOption.Tree1: return tree1Prefab;
            case TreeOption.Tree2: return tree2Prefab;
            case TreeOption.Tree3: return tree3Prefab;
            default: return tree1Prefab;
        }
    }

    private string GetQuestIdForTree(TreeOption option)
    {
        switch (option)
        {
            case TreeOption.Tree1: return tree1QuestId;
            case TreeOption.Tree2: return tree2QuestId;
            case TreeOption.Tree3: return tree3QuestId;
            default: return tree1QuestId;
        }
    }

    private void NotifyQuestForPlantedTree(TreeOption option)
    {
        string specificQuestId = GetQuestIdForTree(option);
        if (!string.IsNullOrWhiteSpace(specificQuestId))
        {
            QuestSystem.AddProgress(specificQuestId, 1);
        }

        if (!string.IsNullOrWhiteSpace(anyTreePlantedQuestId))
        {
            QuestSystem.AddProgress(anyTreePlantedQuestId, 1);
        }
    }

    private void AddTreeScore(TreeOption option)
    {
        int newScore = 0;

        switch (option)
        {
            case TreeOption.Tree1:
                tree1Score += 1;
                newScore = tree1Score;
                break;

            case TreeOption.Tree2:
                tree2Score += 1;
                newScore = tree2Score;
                break;

            case TreeOption.Tree3:
                tree3Score += 1;
                newScore = tree3Score;
                break;
        }

        onTreePlanted?.Invoke(option, newScore);
    }

    private void UseSekopAt(Vector3Int targetCell)
    {
        if (!playerInArea)
            return;

        if (clearableTilemaps != null)
        {
            foreach (Tilemap tilemap in clearableTilemaps)
            {
                if (IsProtectedGround(tilemap)) continue;

                tilemap.SetTile(targetCell, null);
            }
            RecordClearedCell(targetCell);
        }

        if (plantedCells.Remove(targetCell))
        {
            if (spawnedTreesByCell.TryGetValue(targetCell, out GameObject treeObj))
            {
                if (treeObj != null) Destroy(treeObj);
                spawnedTreesByCell.Remove(targetCell);
            }
            UnrecordPlantedTree(targetCell);
        }

        ClearPrefabsOnLayerAtCell(targetCell);

        if (!string.IsNullOrWhiteSpace(sekopQuestId))
        {
            QuestSystem.AddProgress(sekopQuestId, 1);
        }

        HungerSystem hs = GetHungerSystem();
        if (hs != null)
        {
            hs.ReduceHungerForSekop();
        }
    }

    private void ClearPrefabsOnLayerAtCell(Vector3Int targetCell)
    {
        if (plantingTilemap == null)
            return;

        bool useLayerMask = sekopClearLayers.value != 0;
        bool useSortingLayer = filterPrefabsBySortingLayer && sekopClearSortingLayers != null && sekopClearSortingLayers.Length > 0;

        if (!useLayerMask && !useSortingLayer)
            return;

        Vector3 worldCenter = plantingTilemap.GetCellCenterWorld(targetCell);
        HashSet<GameObject> toDestroy = new HashSet<GameObject>();

        if (useLayerMask)
        {
            Collider2D[] hits2D = Physics2D.OverlapCircleAll(worldCenter, sekopPrefabClearRadius, sekopClearLayers);
            foreach (Collider2D col in hits2D)
            {
                if (col == null) continue;
                GameObject candidate = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
                if (candidate != null && candidate != gameObject && MatchesSortingLayer(candidate, useSortingLayer))
                {
                    toDestroy.Add(candidate);
                }
            }

            Collider[] hits3D = Physics.OverlapSphere(worldCenter, sekopPrefabClearRadius, sekopClearLayers);
            foreach (Collider col in hits3D)
            {
                if (col == null) continue;
                GameObject candidate = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
                if (candidate != null && candidate != gameObject && MatchesSortingLayer(candidate, useSortingLayer))
                {
                    toDestroy.Add(candidate);
                }
            }
        }
        else if (useSortingLayer)
        {
            Renderer[] allRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (Renderer rend in allRenderers)
            {
                if (rend == null || rend is TilemapRenderer) continue;
                float reach = sekopPrefabClearRadius + Mathf.Max(rend.bounds.extents.x, rend.bounds.extents.y);
                if (Vector2.Distance(rend.bounds.center, worldCenter) <= reach)
                {
                    if (rend.gameObject != gameObject && HasMatchingSortingLayer(rend))
                    {
                        toDestroy.Add(rend.gameObject);
                    }
                }
            }
        }

        if (toDestroy.Count > 0)
        {
            RecordClearedPrefab(worldCenter);
        }

        foreach (GameObject obj in toDestroy)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
    }

    private bool MatchesSortingLayer(GameObject obj, bool useSortingLayer)
    {
        if (!useSortingLayer) return true;
        Renderer rend = obj.GetComponentInChildren<Renderer>();
        return rend != null && HasMatchingSortingLayer(rend);
    }

    private bool HasMatchingSortingLayer(Renderer rend)
    {
        if (rend == null || sekopClearSortingLayers == null) return false;
        foreach (string layerName in sekopClearSortingLayers)
        {
            if (!string.IsNullOrEmpty(layerName) && rend.sortingLayerName == layerName)
            {
                return true;
            }
        }
        return false;
    }

    private void UseSiramAt(Vector3Int cell)
    {
        if (!playerInArea || plantingTilemap == null)
            return;

        Sprite spriteToUse = waterSprite != null ? waterSprite : defaultHighlightSprite;
        if (spriteToUse == null)
            return;

        Vector3 worldCenter = plantingTilemap.GetCellCenterWorld(cell);
        GameObject waterMarker = new GameObject($"WaterTile_{cell.x}_{cell.y}");
        waterMarker.transform.position = worldCenter;

        if (treeParent != null)
        {
            waterMarker.transform.SetParent(treeParent, true);
        }

        Vector3 cellSize = plantingTilemap.cellSize;
        if (spriteToUse.bounds.size.x > 0f && spriteToUse.bounds.size.y > 0f)
        {
            waterMarker.transform.localScale = new Vector3(
                cellSize.x / spriteToUse.bounds.size.x,
                cellSize.y / spriteToUse.bounds.size.y,
                1f
            );
        }

        SpriteRenderer sr = waterMarker.AddComponent<SpriteRenderer>();
        sr.sprite = spriteToUse;
        sr.color = wateredColor;

        if (highlightRenderer != null)
        {
            sr.sortingLayerID = highlightRenderer.sortingLayerID;
            sr.sortingOrder = highlightRenderer.sortingOrder + waterSortingOrderOffset;
        }

        StartCoroutine(DestroyAfterDelay(waterMarker, waterDuration));

        HungerSystem hs = GetHungerSystem();
        if (hs != null)
        {
            hs.ReduceHungerForSiram();
        }
    }

    private IEnumerator DestroyAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null)
        {
            Destroy(obj);
        }
    }

    private void ClearSingleTileAtCell(Vector3Int targetCell)
    {
        if (clearableTilemaps == null) return;

        foreach (Tilemap tilemap in clearableTilemaps)
        {
            if (IsProtectedGround(tilemap)) continue;

            tilemap.SetTile(targetCell, null);
        }
        RecordClearedCell(targetCell);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInArea = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInArea = false;
            isHoveringValidTile = false;
            if (tileHighlight != null) tileHighlight.SetActive(false);
            ClosePlantPanel();
        }
    }
}