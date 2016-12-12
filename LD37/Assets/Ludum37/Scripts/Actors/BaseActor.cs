﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BaseActor : MonoBehaviour {
    public readonly static Vector3[] kUpAxisMovement =
    {
        Vector3.forward,
        Vector3.right,
        Vector3.back,
        Vector3.left,
    };

    public readonly static Vector3[] kRightAxisMovement =
    {
        Vector3.up,
        Vector3.forward,
        Vector3.down,
        Vector3.back,
    };

    public readonly static Vector3[] kForwardAxisMovement =
    {
        Vector3.up,
        Vector3.right,
        Vector3.down,
        Vector3.left,
    };

    public float m_movementTime = 0.5f;
    public float m_turnSpeed = 180.0f;
    public LevelCell m_groundCell;

    protected Coroutine m_pathFindingCoroutine;

    protected bool m_pathReady = false;
    protected Stack<int> m_path = new Stack<int>();
    protected Dictionary<int, Vector3> m_pathGravities = new Dictionary<int, Vector3>();
    
    protected float m_moveTimer = 0.0f;
    protected float m_actualMoveTime = 1.0f;
    protected Vector3 m_startPosition;
    protected Vector3 m_desiredPosition;
    protected Quaternion m_desiredRotation;
    protected Vector3 m_gravity = Vector3.down;

    protected virtual void Update()
    {
        if(m_moveTimer <= 0)
        {
            if (m_path.Count > 0 && m_pathReady)
            {
                m_startPosition = transform.position;
                int cellIndex = m_path.Pop();
                m_desiredPosition = GetCellWorldPosition(cellIndex);
                m_gravity = m_pathGravities[cellIndex];

                m_actualMoveTime = m_movementTime;

                Vector3 up = -m_gravity;
                LevelCell cell = GetCell(cellIndex);
                if (cell != null && cell.m_data.m_type == LevelCellType.Ramp)
                {
                    if(m_groundCell != null && m_groundCell.m_data.m_type == LevelCellType.Ramp)
                    {
                        m_actualMoveTime *= 1.5f;
                    }
                    m_desiredPosition += up * 0.5f;
                    SetGroundCell(cell);
                }
                else
                {
                    DiscoverGroundCell();
                }

                Vector3 lookDir = (m_desiredPosition - m_startPosition).normalized;
                lookDir = Vector3.Cross(up, lookDir);
                lookDir = Vector3.Cross(lookDir, up);
                m_desiredRotation = Quaternion.LookRotation(lookDir, up);
                Debug.DrawRay(m_desiredPosition, up, Color.red, 5.0f);

                m_moveTimer += m_actualMoveTime;
            }
        }
        else
        {
            m_moveTimer -= Time.deltaTime;
            float t = 1.0f - (m_moveTimer / m_actualMoveTime);
            transform.position = Vector3.Lerp(m_startPosition, m_desiredPosition, t);
        }

        transform.rotation = Quaternion.RotateTowards(transform.rotation, m_desiredRotation, Time.deltaTime * m_turnSpeed);
    }

    public void DiscoverGroundCell()
    {
        SetGroundCell(GetCell(transform.position + m_gravity));
    }

    public void SetGroundCell(LevelCell cell)
    {
        m_groundCell = cell;
        if (m_groundCell != null)
        {
            transform.parent = m_groundCell.transform;
        }
    }

    public LevelCell GetCell(Vector3 worldPos)
    {
        return LevelManager.Instance.m_activeLevel.GetCell(worldPos);
    }

    public LevelCell GetCell(int index)
    {
        return LevelManager.Instance.m_activeLevel.GetCell(index);
    }

    public Vector3 GetCellWorldPosition(int index)
    {
        return LevelManager.Instance.m_activeLevel.GetCellWorldPosition(index);
    }

    public int GetCellIndex(Vector3 worldPos)
    {
        return LevelManager.Instance.m_activeLevel.GetCellIndex(worldPos);
    }

    public int GetCellIndex(LevelCell cell)
    {
        return LevelManager.Instance.m_activeLevel.GetCellIndex(cell);
    }

    public void PathTo(Vector3 destWorldPos)
    {
        if(m_pathFindingCoroutine != null)
        {
            StopCoroutine(m_pathFindingCoroutine);
        }
        m_pathFindingCoroutine = StartCoroutine(CalculatePath(destWorldPos));
    }

    public void ClearPath()
    {
        m_path.Clear();
    }

    protected IEnumerator CalculatePath(Vector3 destWorldPos)
    {
        ClearPath();
        m_pathReady = false;

        int destIndex = GetCellIndex(destWorldPos);
        int startIndex;
        if (m_groundCell != null && m_groundCell.m_data.m_type == LevelCellType.Ramp)
        {
            startIndex = GetCellIndex(m_groundCell);
        }
        else
        {
            startIndex = GetCellIndex(transform.position);
        }

        if (startIndex == destIndex)
        {
            yield break;
        }
        Debug.Log("pathfinding started");
        //Debug.LogFormat("Pathing to {0}", destWorldPos);
        HashSet<int> closedSet = new HashSet<int>();
        HashSet<int> openSet = new HashSet<int>();
        Dictionary<int, int> cameFrom = new Dictionary<int, int>();
        Dictionary<int, float> gScores = new Dictionary<int, float>();
        Dictionary<int, float> fScores = new Dictionary<int, float>();
        Dictionary<int, Vector3> gravities = new Dictionary<int, Vector3>();

        int currIndex = startIndex;
        Vector3 currPos = GetCellWorldPosition(currIndex);
        Vector3 currGravity = m_gravity;

        gravities[currIndex] = currGravity;
        gScores[currIndex] = 0;
        fScores[currIndex] = Vector3.SqrMagnitude(destWorldPos - transform.position);
        openSet.Add(currIndex);

        while (openSet.Count > 0)
        {
            if (Input.GetKey(KeyCode.Escape))
            {
                break;
            }

            float best = float.MaxValue;
            foreach (int index in openSet)
            {
                float fScore = fScores[index];
                if(fScore < best)
                {
                    best = fScore;
                    currIndex = index;
                }
            }

            openSet.Remove(currIndex);

            if(currIndex == destIndex)
            {
                m_pathGravities = gravities;
                // Reach dest! build the path
                while (currIndex != startIndex)
                {
                    if (Input.GetKey(KeyCode.Escape))
                    {
                        break;
                    }
                    m_path.Push(currIndex);
                    Debug.DrawLine(GetCellWorldPosition(currIndex), GetCellWorldPosition(cameFrom[currIndex]), Color.green, 2.0f);
                    currIndex = cameFrom[currIndex];
                    yield return new WaitForEndOfFrame();
                }
                m_pathReady = true;
                yield break;
            }

            currPos = GetCellWorldPosition(currIndex);
            currGravity = gravities[currIndex];
            closedSet.Add(currIndex);

            List<int> possibleMoves = GetPossibleMoves(currPos, currGravity, closedSet, gravities);
            for(int i = 0, n = possibleMoves.Count; i < n; ++i)
            {
                int moveIndex = possibleMoves[i];
                float moveGScore = gScores[currIndex] + 1;
                if(!openSet.Contains(moveIndex))
                {
                    openSet.Add(moveIndex);
                }
                else if(moveGScore >= gScores[moveIndex])
                {
                    continue;
                }

                cameFrom[moveIndex] = currIndex;
                gScores[moveIndex] = moveGScore;
                fScores[moveIndex] = moveGScore + Vector3.SqrMagnitude(destWorldPos - GetCellWorldPosition(moveIndex));
            }

            yield return new WaitForEndOfFrame();
        }
    }
    
    protected List<int> GetPossibleMoves(Vector3 worldPos, Vector3 gravityDir, HashSet<int> visited, Dictionary<int, Vector3> gravities)
    {
        List<int> possibleMoves = new List<int>();

        LevelCell currentCell = GetCell(worldPos);
        if(currentCell != null && currentCell.m_data.m_type == LevelCellType.Ramp)
        {
            AddPossibleMove(possibleMoves, worldPos, -currentCell.transform.forward, -currentCell.transform.up, visited, gravities);
            AddPossibleMove(possibleMoves, worldPos, currentCell.transform.up, currentCell.transform.forward, visited, gravities);
            AddPossibleMove(possibleMoves, worldPos, -currentCell.transform.forward - currentCell.transform.up, gravityDir, visited, gravities);
            AddPossibleMove(possibleMoves, worldPos, currentCell.transform.up + currentCell.transform.forward, gravityDir, visited, gravities);
        }
        else
        {
            Vector3[] dirs;
            if (Mathf.Abs(gravityDir.x) > 0.5f)
            {
                dirs = kRightAxisMovement;
            }
            else if (Mathf.Abs(gravityDir.y) > 0.5f)
            {
                dirs = kUpAxisMovement;
            }
            else
            {
                dirs = kForwardAxisMovement;
            }

            for (int i = 0; i < 4; ++i)
            {
                Vector3 dir = dirs[i];
                AddPossibleMove(possibleMoves, worldPos, dir, gravityDir, visited, gravities);
            }
        }

        return possibleMoves;
    }

    protected void AddPossibleMove(List<int> possibleMoves, Vector3 worldPos, Vector3 dir, Vector3 gravityDir, HashSet<int> visited, Dictionary<int, Vector3> gravities)
    {
        Vector3 checkPos = worldPos + dir;
        int index = GetCellIndex(checkPos);
        if (visited.Contains(index))
        {
            return;
        }

        LevelCell cell = GetCell(checkPos);
        if (cell != null)
        {
            switch (cell.m_data.m_type)
            {
                case LevelCellType.Solid:
                    return;
                case LevelCellType.Ramp:
                    // determine if we can move up on the ramp from here
                    if (Vector3.Dot(dir, cell.transform.up) < -0.5f || Vector3.Dot(dir, -cell.transform.forward) < -0.5f)
                    {
                        gravities[index] = -Vector3.Lerp(cell.transform.up, -cell.transform.forward, 0.5f).normalized;
                        possibleMoves.Add(index);
                    }
                    return;
            }
        }

        LevelCell below = GetCell(checkPos + gravityDir);
        if (below != null)
        {
            if (below.m_data.m_type == LevelCellType.Solid)
            {
                gravities[index] = gravityDir;
                possibleMoves.Add(index);
            }
            else if (below.m_data.m_type == LevelCellType.Ramp)
            {
                index = GetCellIndex(below);
                if(!visited.Contains(index))
                {
                    // determine if we can move up on the ramp from here
                    if (Vector3.Dot(dir, below.transform.up) > 0.5f || Vector3.Dot(dir, -below.transform.forward) > 0.5f)
                    {
                        gravities[index] = -Vector3.Lerp(below.transform.up, -below.transform.forward, 0.5f).normalized;
                        possibleMoves.Add(index);
                    }
                }                
            }
        }
    }
}