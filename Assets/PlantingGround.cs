using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;

public class PlantingScript : MonoBehaviour
{
    [Header("Tilemap & Tree Prefabs")]
    public Tilemap plantingTilemap;
    public Transform treeParent; // Optional empty GameObject to keep spawned trees tidy
    public Vector3 spawnOffset = Vector3.zero; // Adjust in Inspector (e.g., X = -3) to align spawned trees
    public GameObject tree1Prefab;
    public GameObject tree2Prefab;
    public GameObject tree3Prefab;

    [Header("Cursor Highlight")]
    public Camera mainCamera;
    public GameObject tileHighlight; // World-space square sprite that snaps to hovered tile
    public Color validTileColor = new Color(0.3f, 1f, 0.3f, 0.45f);   // Semi-transparent green
    public Color occupiedTileColor = new Color(1f, 0.3f, 0.3f, 0.45f); // Semi-transparent red

    [Header("UI & Player Detection")]
    public GameObject plantPanel; // Your panel with 3 tree buttons (starts inactive)
    public string playerTag = "Player";

    private readonly HashSet<Vector3Int> plantedCells = new HashSet<Vector3Int>();
    private SpriteRenderer highlightRenderer;
    private Vector3Int hoveredCell;
    private Vector3Int lockedCell;
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

        if (tileHighlight != null)
        {
            highlightRenderer = tileHighlight.GetComponent<SpriteRenderer>();
            tileHighlight.SetActive(false);
        }

        if (plantPanel != null)
        {
            plantPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (!playerInArea)
        {
            if (tileHighlight != null && tileHighlight.activeSelf)
                tileHighlight.SetActive(false);
            return;
        }

        // Only update cursor hover when the UI panel is closed
        if (!isPanelOpen)
        {
            UpdateCursorHighlight();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (isPanelOpen)
            {
                ClosePlantPanel();
            }
            else if (isHoveringValidTile && !plantedCells.Contains(hoveredCell))
            {
                // Lock the hovered tile before opening UI so moving mouse to buttons won't change it
                lockedCell = hoveredCell;
                OpenPlantPanel();
            }
        }
    }

    private void UpdateCursorHighlight()
    {
        if (mainCamera == null || plantingTilemap == null || tileHighlight == null)
            return;

        // Don't highlight through UI elements
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

        if (plantingTilemap.HasTile(cellPos))
        {
            hoveredCell = cellPos;
            isHoveringValidTile = true;

            tileHighlight.SetActive(true);
            tileHighlight.transform.position = plantingTilemap.GetCellCenterWorld(cellPos);

            if (highlightRenderer != null)
            {
                highlightRenderer.color = plantedCells.Contains(cellPos)
                    ? occupiedTileColor
                    : validTileColor;
            }
        }
        else
        {
            isHoveringValidTile = false;
            tileHighlight.SetActive(false);
        }
    }

    public void OpenPlantPanel()
    {
        if (plantPanel == null) return;
        isPanelOpen = true;
        plantPanel.SetActive(true);
    }

    public void ClosePlantPanel()
    {
        isPanelOpen = false;
        if (plantPanel != null)
        {
            plantPanel.SetActive(false);
        }
    }

    // Hook Button 1 -> OnClick() to this
    public void PlantTree1()
    {
        PlantTree(tree1Prefab);
    }

    // Hook Button 2 -> OnClick() to this
    public void PlantTree2()
    {
        PlantTree(tree2Prefab);
    }

    // Hook Button 3 -> OnClick() to this
    public void PlantTree3()
    {
        PlantTree(tree3Prefab);
    }

    private void PlantTree(GameObject treePrefab)
    {
        if (!playerInArea || plantingTilemap == null || treePrefab == null)
            return;

        if (!plantingTilemap.HasTile(lockedCell) || plantedCells.Contains(lockedCell))
            return;

        Vector3 spawnPos = plantingTilemap.GetCellCenterWorld(lockedCell) + spawnOffset;
        Instantiate(treePrefab, spawnPos, Quaternion.identity, treeParent);
        plantedCells.Add(lockedCell);

        ClosePlantPanel();
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