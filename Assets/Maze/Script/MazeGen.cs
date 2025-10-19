using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class MazeGenerator : MonoBehaviour
{
    [Header("Maze Settings")]
    public int width = 20;
    public int height = 20;
    public float cellSize = 4f;

    [Header("Escape Settings")]
    [Range(0f, 1f)]
    [Tooltip("0 = Close to spawn, 1 = Far from spawn (uses percentage of max maze distance)")]
    public float escapeGateBuffer = 0.5f;

    [Header("Prefabs")] 
    public GameObject[] wallPrefabs;
    public GameObject[] floorPrefabs;
    public GameObject escapeDoorPrefab;
    public GameObject[] decorationPrefabs;

    [Header("Generation Settings")]
    public bool generateOnStart = true;
    public bool randomSeed = true;
    public int seed = 0;
    [Range(0f, 0.3f)] public float loopChance = 0.05f;
    [Tooltip("Radius around player spawn to keep clear of walls")]
    public float spawnClearRadius = 5f;
    
    [Header("Height Adjustment")]
    [Tooltip("Y position for floor prefabs")]
    public float floorYPosition = 0f;
    [Tooltip("Auto-adjust walls so their bottom sits at this Y level")]
    public float wallGroundLevel = 0f;

    [Header("Decoration Settings")]
    [Range(0f, 1f)]
    public float decorationDensity = 0.1f;
    [Tooltip("Minimum distance between decorations")]
    public float decorationSpacing = 1f;
    [Tooltip("Random rotation range for decorations (degrees)")]
    public float maxDecorationRotation = 360f;
    [Tooltip("Random scale variation for decorations")]
    public Vector2 decorationScaleRange = new Vector2(0.8f, 1.2f);

    [Header("Portal Movement Settings")]
    [Tooltip("Time window in seconds during which portal can move if player gets close")]
    public float portalMoveWindow = 30f;
    [Tooltip("Distance threshold - portal moves if player gets this close")]
    public float portalProximityThreshold = 10f;

    private GameObject escapeDoorInstance;
    private bool portalMoved = false;
    private float timeSinceStart = 0f;
    private Transform player;
    private Cell[,] cells;
    private System.Random rng;
    private Transform mazeParent;
    private Transform playerSpawn;
    private Vector3 mazeOrigin;
    private GameObject[] floorObjects;
    private Dictionary<GameObject, float> wallOffsetCache = new Dictionary<GameObject, float>();
    private Dictionary<GameObject, float> decorationOffsetCache = new Dictionary<GameObject, float>();
    
    private HashSet<Vector3> generatedWallPositions = new HashSet<Vector3>();
    // Track which floor positions have been used to prevent overlapping floors
    private HashSet<Vector2Int> generatedFloorPositions = new HashSet<Vector2Int>();
    // Track decoration positions to maintain spacing
    private HashSet<Vector3> decorationPositions = new HashSet<Vector3>();

    private class Cell
    {
        public bool visited;
        public bool north = true, south = true, east = true, west = true;
    }

    private void Start()
    {
        if (generateOnStart) GenerateMaze();
    }

    [ContextMenu("Generate Maze")]
    public void GenerateMaze()
    {
        playerSpawn = GameObject.FindGameObjectWithTag("PlayerSpawn")?.transform;
        if (playerSpawn == null)
        {
            Debug.LogError("No object tagged 'PlayerSpawn' found.");
            return;
        }

        // Find the actual player object (assumes it has tag "Player")
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
        else
        {
            Debug.LogWarning("No object tagged 'Player' found. Portal movement will use PlayerSpawn position.");
            player = playerSpawn;
        }

        // Initialize seed
        if (randomSeed || seed == 0)
            seed = Random.Range(int.MinValue, int.MaxValue);
        rng = new System.Random(seed);

        // Clean up previous maze
        if (mazeParent != null) DestroyImmediate(mazeParent.gameObject);
        mazeParent = new GameObject("GeneratedMaze").transform;
        mazeParent.SetParent(transform);

        // Clear tracking for new generation
        wallOffsetCache.Clear();
        decorationOffsetCache.Clear();
        generatedWallPositions.Clear();
        generatedFloorPositions.Clear();
        decorationPositions.Clear();
        
        // Reset portal movement tracking
        timeSinceStart = 0f;
        portalMoved = false;
        escapeDoorInstance = null;

        // Pre-calculate offsets for all prefabs
        CalculateWallOffsets();
        CalculateDecorationOffsets();

        // Initialize cells array
        cells = new Cell[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                cells[x, y] = new Cell();

        // Generate maze structure
        Vector2Int startCell = new Vector2Int(rng.Next(width), rng.Next(height));
        Carve(startCell);
        AddLoops();

        // Build physical maze
        mazeOrigin = playerSpawn.position - new Vector3(width * cellSize * 0.5f, 0, height * cellSize * 0.5f);
        BuildMaze();
        
        // Place decorations
        if (decorationPrefabs.Length > 0 && decorationDensity > 0f)
            PlaceDecorations();
        
        // Place escape
        if (escapeDoorPrefab != null)
            PlaceEscapeDoor();
    }

    private void CalculateWallOffsets()
    {
        foreach (var prefab in wallPrefabs)
        {
            if (prefab == null) continue;

            // Get bounds from all renderers in the prefab
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                wallOffsetCache[prefab] = 0f;
                Debug.LogWarning($"Wall prefab '{prefab.name}' has no renderers. Offset set to 0.");
                continue;
            }

            // Calculate combined bounds
            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combinedBounds.Encapsulate(renderers[i].bounds);
            }

            // Calculate offset needed to place bottom at ground level
            // Local bounds min Y relative to prefab origin
            float localMinY = combinedBounds.min.y - prefab.transform.position.y;
            float offset = wallGroundLevel - localMinY;
            
            wallOffsetCache[prefab] = offset;
            
            Debug.Log($"Wall prefab '{prefab.name}': bounds min Y = {localMinY:F2}, offset = {offset:F2}");
        }

        // Calculate offset for escape door if it exists
        if (escapeDoorPrefab != null)
        {
            Renderer[] renderers = escapeDoorPrefab.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds combinedBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    combinedBounds.Encapsulate(renderers[i].bounds);
                }
                
                float localMinY = combinedBounds.min.y - escapeDoorPrefab.transform.position.y;
                float offset = wallGroundLevel - localMinY;
                wallOffsetCache[escapeDoorPrefab] = offset;
            }
            else
            {
                wallOffsetCache[escapeDoorPrefab] = 0f;
            }
        }
    }

    private void CalculateDecorationOffsets()
    {
        foreach (var prefab in decorationPrefabs)
        {
            if (prefab == null) continue;

            // Get bounds from all renderers in the prefab
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                decorationOffsetCache[prefab] = 0f;
                Debug.LogWarning($"Decoration prefab '{prefab.name}' has no renderers. Offset set to 0.");
                continue;
            }

            // Calculate combined bounds
            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combinedBounds.Encapsulate(renderers[i].bounds);
            }

            // Calculate offset needed to place bottom at ground level
            float localMinY = combinedBounds.min.y - prefab.transform.position.y;
            float offset = -localMinY;
            
            decorationOffsetCache[prefab] = offset;
            
            Debug.Log($"Decoration prefab '{prefab.name}': bounds min Y = {localMinY:F2}, offset = {offset:F2}");
        }
    }

    private void Carve(Vector2Int start)
    {
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        cells[start.x, start.y].visited = true;
        stack.Push(start);

        while (stack.Count > 0)
        {
            var current = stack.Peek();
            var neighbors = GetUnvisitedNeighbors(current);
            
            if (neighbors.Count == 0)
            {
                stack.Pop();
                continue;
            }

            var chosen = neighbors[rng.Next(neighbors.Count)];
            RemoveWall(current, chosen);
            cells[chosen.x, chosen.y].visited = true;
            stack.Push(chosen);
        }
    }

    private List<Vector2Int> GetUnvisitedNeighbors(Vector2Int cell)
    {
        List<Vector2Int> list = new List<Vector2Int>();
        int x = cell.x, y = cell.y;
        
        if (y + 1 < height && !cells[x, y + 1].visited) list.Add(new Vector2Int(x, y + 1));
        if (y - 1 >= 0 && !cells[x, y - 1].visited) list.Add(new Vector2Int(x, y - 1));
        if (x + 1 < width && !cells[x + 1, y].visited) list.Add(new Vector2Int(x + 1, y));
        if (x - 1 >= 0 && !cells[x - 1, y].visited) list.Add(new Vector2Int(x - 1, y));
        
        return list;
    }

    private void RemoveWall(Vector2Int a, Vector2Int b)
    {
        int dx = b.x - a.x;
        int dy = b.y - a.y;
        
        if (dx == 1) { cells[a.x, a.y].east = false; cells[b.x, b.y].west = false; }
        else if (dx == -1) { cells[a.x, a.y].west = false; cells[b.x, b.y].east = false; }
        else if (dy == 1) { cells[a.x, a.y].north = false; cells[b.x, b.y].south = false; }
        else if (dy == -1) { cells[a.x, a.y].south = false; cells[b.x, b.y].north = false; }
    }

    private void AddLoops()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (rng.NextDouble() < loopChance)
                {
                    if (x + 1 < width && rng.NextDouble() > 0.5f)
                        RemoveWall(new Vector2Int(x, y), new Vector2Int(x + 1, y));
                    else if (y + 1 < height)
                        RemoveWall(new Vector2Int(x, y), new Vector2Int(x, y + 1));
                }
            }
        }
    }

    private void BuildMaze()
    {
        floorObjects = new GameObject[width * height];
        int floorIndex = 0;

        // First pass: Create all floors without overlapping
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 cellCenter = mazeOrigin + new Vector3(x * cellSize, floorYPosition, y * cellSize);
                
                // Only create floor if this position hasn't been used yet
                Vector2Int floorPos = new Vector2Int(x, y);
                if (!generatedFloorPositions.Contains(floorPos))
                {
                    generatedFloorPositions.Add(floorPos);
                    
                    // Instantiate floor at specified Y position
                    if (floorPrefabs.Length > 0)
                    {
                        var floorPrefab = floorPrefabs[rng.Next(floorPrefabs.Length)];
                        var floor = Instantiate(floorPrefab, cellCenter, Quaternion.identity, mazeParent);
                        
                        // Scale only X and Z to match cell size, preserve Y scale
                        Vector3 originalScale = floorPrefab.transform.localScale;
                        floor.transform.localScale = new Vector3(cellSize, originalScale.y, cellSize);
                        
                        floorObjects[floorIndex++] = floor;
                    }
                }
            }
        }

        // Second pass: Create walls with duplicate prevention
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 cellCenter = mazeOrigin + new Vector3(x * cellSize, floorYPosition, y * cellSize);

                // Only generate walls where they should exist
                if (wallPrefabs.Length > 0)
                {
                    // North wall - only generate if this cell has a north wall
                    if (cells[x, y].north)
                        TrySpawnWall(cellCenter + new Vector3(0, 0, cellSize * 0.5f), Quaternion.identity);
                    
                    // South wall - only generate if this cell has a south wall
                    if (cells[x, y].south)
                        TrySpawnWall(cellCenter + new Vector3(0, 0, -cellSize * 0.5f), Quaternion.identity);
                    
                    // East wall - only generate if this cell has an east wall
                    if (cells[x, y].east)
                        TrySpawnWall(cellCenter + new Vector3(cellSize * 0.5f, 0, 0), Quaternion.Euler(0, 90, 0));
                    
                    // West wall - only generate if this cell has a west wall
                    if (cells[x, y].west)
                        TrySpawnWall(cellCenter + new Vector3(-cellSize * 0.5f, 0, 0), Quaternion.Euler(0, 90, 0));
                }
            }
        }
    }

    private void TrySpawnWall(Vector3 position, Quaternion rotation)
    {
        // Don't spawn walls in player spawn zone
        if (Vector3.Distance(position, playerSpawn.position) < spawnClearRadius)
            return;

        // Create a position key with tolerance for floating point errors
        Vector3 positionKey = new Vector3(
            Mathf.Round(position.x * 100f) / 100f,
            Mathf.Round(position.y * 100f) / 100f,
            Mathf.Round(position.z * 100f) / 100f
        );

        // Check if we've already generated a wall at this position
        if (generatedWallPositions.Contains(positionKey))
            return;

        // Mark this position as used
        generatedWallPositions.Add(positionKey);

        var prefab = wallPrefabs[rng.Next(wallPrefabs.Length)];
        
        // Get the pre-calculated offset for this prefab
        float heightOffset = wallOffsetCache.ContainsKey(prefab) ? wallOffsetCache[prefab] : 0f;
        
        // Apply height offset to raise wall so bottom sits at floor level
        Vector3 adjustedPosition = position + Vector3.up * heightOffset;
        var wall = Instantiate(prefab, adjustedPosition, rotation, mazeParent);
        
        // Snap to floor tagged MazeFloor if needed
        RaycastHit hit;
        if (Physics.Raycast(position + Vector3.up * 10f, Vector3.down, out hit, 20f))
        {
            if (hit.collider.CompareTag("MazeFloor"))
            {
                wall.transform.position = hit.point + Vector3.up * heightOffset;
            }
        }
    }

    private void PlaceDecorations()
    {
        int totalPossibleDecorations = width * height;
        int targetDecorationCount = Mathf.RoundToInt(totalPossibleDecorations * decorationDensity);
        int placedDecorations = 0;

        // Try to place decorations throughout the maze
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Skip if we've reached our target count
                if (placedDecorations >= targetDecorationCount)
                    break;

                // Random chance to place decoration in this cell based on density
                if (rng.NextDouble() > decorationDensity)
                    continue;

                Vector3 cellCenter = mazeOrigin + new Vector3(x * cellSize, floorYPosition, y * cellSize);
                
                // Generate random position within the cell (avoiding edges)
                float randomX = (float)(rng.NextDouble() * 0.6f - 0.3f) * cellSize;
                float randomZ = (float)(rng.NextDouble() * 0.6f - 0.3f) * cellSize;
                Vector3 decorationPos = cellCenter + new Vector3(randomX, 0, randomZ);

                // Don't place decorations in player spawn zone
                if (Vector3.Distance(decorationPos, playerSpawn.position) < spawnClearRadius)
                    continue;

                // Check if this position is too close to existing decorations
                if (IsTooCloseToExistingDecoration(decorationPos))
                    continue;

                // Check if this position is in a walkable area (not blocked by walls)
                if (!IsPositionWalkable(new Vector2Int(x, y), decorationPos))
                    continue;

                // Place the decoration
                if (TryPlaceDecoration(decorationPos))
                {
                    placedDecorations++;
                }
            }
        }

        Debug.Log($"Placed {placedDecorations} decorations in the maze");
    }

    private bool IsTooCloseToExistingDecoration(Vector3 position)
    {
        foreach (var existingPos in decorationPositions)
        {
            if (Vector3.Distance(position, existingPos) < decorationSpacing)
                return true;
        }
        return false;
    }

    private bool IsPositionWalkable(Vector2Int cell, Vector3 worldPos)
    {
        // Check if the position is too close to walls
        Vector3 cellCenter = mazeOrigin + new Vector3(cell.x * cellSize, 0, cell.y * cellSize);
        
        // Calculate distance from cell center to check if we're near walls
        float distFromCenterX = Mathf.Abs(worldPos.x - cellCenter.x);
        float distFromCenterZ = Mathf.Abs(worldPos.z - cellCenter.z);
        
        // If we're too close to where walls would be, don't place decoration
        float wallBuffer = cellSize * 0.4f;
        if (distFromCenterX > wallBuffer || distFromCenterZ > wallBuffer)
            return false;

        return true;
    }

    private bool TryPlaceDecoration(Vector3 position)
    {
        if (decorationPrefabs.Length == 0) return false;

        var prefab = decorationPrefabs[rng.Next(decorationPrefabs.Length)];
        
        // Get the pre-calculated offset for this prefab
        float heightOffset = decorationOffsetCache.ContainsKey(prefab) ? decorationOffsetCache[prefab] : 0f;
        
        // Random rotation
        Quaternion randomRotation = Quaternion.Euler(0, (float)rng.NextDouble() * maxDecorationRotation, 0);
        
        // Random scale
        float randomScale = Mathf.Lerp(decorationScaleRange.x, decorationScaleRange.y, (float)rng.NextDouble());
        
        // Apply height offset
        Vector3 adjustedPosition = position + Vector3.up * heightOffset;
        
        var decoration = Instantiate(prefab, adjustedPosition, randomRotation, mazeParent);
        decoration.transform.localScale *= randomScale;
        
        // Snap to floor
        RaycastHit hit;
        if (Physics.Raycast(position + Vector3.up * 10f, Vector3.down, out hit, 20f))
        {
            if (hit.collider.CompareTag("MazeFloor"))
            {
                decoration.transform.position = hit.point + Vector3.up * heightOffset;
            }
        }

        // Record this position
        decorationPositions.Add(position);
        
        return true;
    }

    private void PlaceEscapeDoor()
    {
        if (escapeDoorPrefab == null) return;

        List<Vector2Int> validCells = new List<Vector2Int>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 worldPos = mazeOrigin + new Vector3(x * cellSize, floorYPosition, y * cellSize);
                float distanceFromSpawn = Vector3.Distance(worldPos, playerSpawn.position);

                // Skip positions too close to player
                if (distanceFromSpawn < spawnClearRadius)
                    continue;

                validCells.Add(new Vector2Int(x, y));
            }
        }

        if (validCells.Count == 0)
        {
            Debug.LogError("No valid positions for escape door found!");
            return;
        }

        // Pick a random valid cell
        Vector2Int chosenCell = validCells[rng.Next(validCells.Count)];
        Vector3 doorPos = mazeOrigin + new Vector3(chosenCell.x * cellSize, floorYPosition, chosenCell.y * cellSize);

        // Apply prefab offset
        float heightOffset = wallOffsetCache.ContainsKey(escapeDoorPrefab) ? wallOffsetCache[escapeDoorPrefab] : 0f;
        doorPos += Vector3.up * heightOffset;

        // Instantiate escape door and store reference
        escapeDoorInstance = Instantiate(escapeDoorPrefab, doorPos, Quaternion.identity, mazeParent);

        // Snap to floor if available
        if (Physics.Raycast(doorPos + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f))
        {
            if (hit.collider.CompareTag("MazeFloor"))
                escapeDoorInstance.transform.position = hit.point + Vector3.up * heightOffset;
        }

        Debug.Log($"Escape gate placed at cell ({chosenCell.x}, {chosenCell.y}), distance from player: {Vector3.Distance(doorPos, playerSpawn.position):F2} units");
    }

    private void Update()
    {
        // If player reference is lost, try to find it again
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
            else
                return; // Can't track without player reference
        }

        timeSinceStart += Time.deltaTime;

        if (!portalMoved && timeSinceStart <= portalMoveWindow && escapeDoorInstance != null)
        {
            float distanceToPortal = Vector3.Distance(player.position, escapeDoorInstance.transform.position);
            
            // Debug info - remove this line once it's working
            if (timeSinceStart % 1f < Time.deltaTime) // Log every second
            {
                Debug.Log($"Time: {timeSinceStart:F1}s / {portalMoveWindow}s, Distance to portal: {distanceToPortal:F1} units (threshold: {portalProximityThreshold})");
            }
            
            if (distanceToPortal < portalProximityThreshold)
            {
                MoveEscapeDoor();
                portalMoved = true;
                Debug.Log("Escape portal moved because player got too close!");
            }
        }
    }

    private void MoveEscapeDoor()
    {
        if (escapeDoorPrefab == null || mazeParent == null || player == null) return;

        // Destroy the old portal
        Destroy(escapeDoorInstance);

        // Get all valid cells again (far from player's CURRENT position)
        List<Vector2Int> validCells = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 worldPos = mazeOrigin + new Vector3(x * cellSize, floorYPosition, y * cellSize);
                // Must be far from player's current position
                if (Vector3.Distance(worldPos, player.position) >= spawnClearRadius * 2f)
                {
                    validCells.Add(new Vector2Int(x, y));
                }
            }
        }

        // Pick a random cell that's far enough from player
        if (validCells.Count == 0)
        {
            Debug.LogWarning("No valid cells found far enough from player for portal relocation!");
            return;
        }
        
        Vector2Int newCell = validCells[rng.Next(validCells.Count)];
        Vector3 newDoorPos = mazeOrigin + new Vector3(newCell.x * cellSize, floorYPosition, newCell.y * cellSize);

        // Apply prefab height offset
        float heightOffset = wallOffsetCache.ContainsKey(escapeDoorPrefab) ? wallOffsetCache[escapeDoorPrefab] : 0f;
        newDoorPos += Vector3.up * heightOffset;

        // Instantiate new escape door
        escapeDoorInstance = Instantiate(escapeDoorPrefab, newDoorPos, Quaternion.identity, mazeParent);

        // Snap to floor if available
        if (Physics.Raycast(newDoorPos + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f))
        {
            if (hit.collider.CompareTag("MazeFloor"))
                escapeDoorInstance.transform.position = hit.point + Vector3.up * heightOffset;
        }
        
        Debug.Log($"Portal relocated to cell ({newCell.x}, {newCell.y}), new distance from player: {Vector3.Distance(newDoorPos, player.position):F2} units");
    }

    private void CreateEdgeEscape()
    {
        // This method is no longer used - keeping for backwards compatibility
        Debug.LogWarning("CreateEdgeEscape() is deprecated. Please assign an escapeDoorPrefab instead.");
    }
}