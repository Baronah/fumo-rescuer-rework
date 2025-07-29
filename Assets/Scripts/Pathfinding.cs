// A* Pathfinding Grid System (Updated with better performance)
using System.Collections.Generic;
using UnityEngine;

public class PathfindingGrid
{
    private Dictionary<Vector2Int, bool> obstacleGrid = new Dictionary<Vector2Int, bool>();
    private float cellSize;
    private LayerMask obstacleLayer;
    private Vector2 lastUpdateCenter = Vector2.zero;
    private float lastUpdateRadius = 0f;

    public PathfindingGrid(float cellSize, LayerMask obstacleLayer)
    {
        this.cellSize = cellSize;
        this.obstacleLayer = obstacleLayer;
    }

    public Vector2Int WorldToGrid(Vector2 worldPos)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldPos.x / cellSize),
            Mathf.RoundToInt(worldPos.y / cellSize)
        );
    }

    public Vector2 GridToWorld(Vector2Int gridPos)
    {
        return new Vector2(gridPos.x * cellSize, gridPos.y * cellSize);
    }

    public void UpdateGrid(Vector2 center, float radius)
    {
        // Only update if we've moved significantly
        if (Vector2.Distance(center, lastUpdateCenter) < cellSize * 2f && Mathf.Abs(radius - lastUpdateRadius) < cellSize)
            return;

        lastUpdateCenter = center;
        lastUpdateRadius = radius;

        // Clear old data outside radius (with some buffer)
        List<Vector2Int> toRemove = new List<Vector2Int>();
        float cleanupRadius = radius + cellSize * 5f; // Add buffer

        foreach (var kvp in obstacleGrid)
        {
            Vector2 worldPos = GridToWorld(kvp.Key);
            if (Vector2.Distance(worldPos, center) > cleanupRadius)
            {
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var key in toRemove)
        {
            obstacleGrid.Remove(key);
        }

        // Add new grid cells
        int gridRadius = Mathf.CeilToInt(radius / cellSize);
        Vector2Int centerGrid = WorldToGrid(center);

        for (int x = -gridRadius; x <= gridRadius; x++)
        {
            for (int y = -gridRadius; y <= gridRadius; y++)
            {
                Vector2Int gridPos = centerGrid + new Vector2Int(x, y);

                if (!obstacleGrid.ContainsKey(gridPos))
                {
                    Vector2 worldPos = GridToWorld(gridPos);
                    bool isObstacle = Physics2D.OverlapCircle(worldPos, cellSize * 0.3f, obstacleLayer) != null;
                    obstacleGrid[gridPos] = isObstacle;
                }
            }
        }
    }

    public bool IsObstacle(Vector2Int gridPos)
    {
        return obstacleGrid.TryGetValue(gridPos, out bool isObstacle) && isObstacle;
    }

    public List<Vector2> FindPath(Vector2 start, Vector2 target, bool allowDiagonal = true)
    {
        Vector2Int startGrid = WorldToGrid(start);
        Vector2Int targetGrid = WorldToGrid(target);

        // If start or target are obstacles, try to find nearby free cells
        if (IsObstacle(startGrid))
        {
            startGrid = FindNearestFreeCell(startGrid);
        }
        if (IsObstacle(targetGrid))
        {
            targetGrid = FindNearestFreeCell(targetGrid);
        }

        // A* algorithm with improved performance
        var openSet = new List<AStarNode>();
        var closedSet = new HashSet<Vector2Int>();
        var allNodes = new Dictionary<Vector2Int, AStarNode>();

        var startNode = new AStarNode
        {
            position = startGrid,
            gCost = 0,
            hCost = GetDistance(startGrid, targetGrid),
            parent = null
        };
        startNode.fCost = startNode.gCost + startNode.hCost;

        openSet.Add(startNode);
        allNodes[startGrid] = startNode;

        int maxIterations = 1000; // Prevent infinite loops
        int iterations = 0;

        while (openSet.Count > 0 && iterations < maxIterations)
        {
            iterations++;

            // Find node with lowest fCost
            AStarNode currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].fCost < currentNode.fCost ||
                    (openSet[i].fCost == currentNode.fCost && openSet[i].hCost < currentNode.hCost))
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode.position);

            // Found target
            if (currentNode.position == targetGrid)
            {
                List<Vector2> rawPath = RetracePath(startNode, currentNode);
                return SmoothPath(rawPath); // Apply path smoothing
            }

            // Check neighbors
            foreach (Vector2Int neighbor in GetNeighbors(currentNode.position, allowDiagonal))
            {
                if (IsObstacle(neighbor) || closedSet.Contains(neighbor))
                    continue;

                // Add penalty for corners and tight spaces
                int moveCost = GetDistance(currentNode.position, neighbor);
                if (IsNearCorner(neighbor))
                {
                    moveCost += 5; // Add penalty for corner positions
                }

                int newGCost = currentNode.gCost + moveCost;

                if (!allNodes.ContainsKey(neighbor))
                {
                    var neighborNode = new AStarNode
                    {
                        position = neighbor,
                        gCost = newGCost,
                        hCost = GetDistance(neighbor, targetGrid),
                        parent = currentNode
                    };
                    neighborNode.fCost = neighborNode.gCost + neighborNode.hCost;

                    allNodes[neighbor] = neighborNode;
                    openSet.Add(neighborNode);
                }
                else if (newGCost < allNodes[neighbor].gCost)
                {
                    var neighborNode = allNodes[neighbor];
                    neighborNode.gCost = newGCost;
                    neighborNode.fCost = neighborNode.gCost + neighborNode.hCost;
                    neighborNode.parent = currentNode;

                    if (!openSet.Contains(neighborNode))
                        openSet.Add(neighborNode);
                }
            }
        }

        // No path found
        return new List<Vector2>();
    }

    private bool IsNearCorner(Vector2Int gridPos)
    {
        // Check if this position is near a corner (has obstacles diagonally adjacent)
        int obstacleCount = 0;
        Vector2Int[] diagonals = {
            new Vector2Int(1, 1), new Vector2Int(1, -1),
            new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        foreach (Vector2Int diagonal in diagonals)
        {
            if (IsObstacle(gridPos + diagonal))
                obstacleCount++;
        }

        return obstacleCount >= 2; // Consider it a corner if 2+ diagonal obstacles
    }

    private List<Vector2> SmoothPath(List<Vector2> rawPath)
    {
        if (rawPath.Count <= 2) return rawPath;

        List<Vector2> smoothedPath = new List<Vector2>();
        smoothedPath.Add(rawPath[0]); // Always keep start point

        int currentIndex = 0;
        while (currentIndex < rawPath.Count - 1)
        {
            int farthestReachable = currentIndex;

            // Find the farthest point we can reach directly
            for (int i = currentIndex + 1; i < rawPath.Count; i++)
            {
                Vector2 direction = (rawPath[i] - rawPath[currentIndex]).normalized;
                float distance = Vector2.Distance(rawPath[currentIndex], rawPath[i]);

                // Check if we can reach this point directly
                RaycastHit2D hit = Physics2D.Raycast(rawPath[currentIndex], direction, distance, obstacleLayer);
                if (hit.collider == null)
                {
                    farthestReachable = i;
                }
                else
                {
                    break; // Can't reach further, stop here
                }
            }

            // Move to the farthest reachable point
            if (farthestReachable > currentIndex)
            {
                currentIndex = farthestReachable;
                smoothedPath.Add(rawPath[currentIndex]);
            }
            else
            {
                // Can't smooth further, add next point
                currentIndex++;
                if (currentIndex < rawPath.Count)
                    smoothedPath.Add(rawPath[currentIndex]);
            }
        }

        return smoothedPath;
    }

    private Vector2Int FindNearestFreeCell(Vector2Int gridPos)
    {
        for (int radius = 1; radius <= 5; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    Vector2Int testPos = gridPos + new Vector2Int(x, y);
                    if (!IsObstacle(testPos))
                    {
                        return testPos;
                    }
                }
            }
        }
        return gridPos; // Fallback to original position
    }

    private List<Vector2Int> GetNeighbors(Vector2Int gridPos, bool allowDiagonal)
    {
        var neighbors = new List<Vector2Int>();

        // Orthogonal neighbors
        neighbors.Add(gridPos + Vector2Int.up);
        neighbors.Add(gridPos + Vector2Int.down);
        neighbors.Add(gridPos + Vector2Int.left);
        neighbors.Add(gridPos + Vector2Int.right);

        if (allowDiagonal)
        {
            neighbors.Add(gridPos + Vector2Int.up + Vector2Int.right);
            neighbors.Add(gridPos + Vector2Int.up + Vector2Int.left);
            neighbors.Add(gridPos + Vector2Int.down + Vector2Int.right);
            neighbors.Add(gridPos + Vector2Int.down + Vector2Int.left);
        }

        return neighbors;
    }

    private int GetDistance(Vector2Int a, Vector2Int b)
    {
        int dstX = Mathf.Abs(a.x - b.x);
        int dstY = Mathf.Abs(a.y - b.y);

        // Diagonal distance calculation
        if (dstX > dstY)
            return 14 * dstY + 10 * (dstX - dstY);
        return 14 * dstX + 10 * (dstY - dstX);
    }

    private List<Vector2> RetracePath(AStarNode startNode, AStarNode endNode)
    {
        var path = new List<Vector2>();
        var currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(GridToWorld(currentNode.position));
            currentNode = currentNode.parent;
        }

        path.Reverse();
        return path;
    }
}

public class AStarNode
{
    public Vector2Int position;
    public int gCost; // Distance from start
    public int hCost; // Distance to target (heuristic)
    public int fCost; // gCost + hCost
    public AStarNode parent;
}