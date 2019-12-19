/*
 * TODO Create
 */


using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aura2API;
using UnityEngine;

public class Volume : MonoBehaviour
{   
    
    /// <summary>
    /// Defines a set of actions that a cell can experience
    /// </summary>
    public enum CellActionID {Destroy, Create, Idle, IgnorePos}

    /// <summary>
    /// A Cell action can be described with the position of the cell,
    /// and the the action that needs to occur
    /// </summary>
    private struct CellAction 
    {
        /// <summary>
        /// The position of the cell inside the grid
        /// </summary>
        public readonly Vector3 position;

        /// <summary>
        /// Action that should be applied to the cell
        /// </summary>
        public readonly CellActionID action;

        public CellAction(Vector3 position, CellActionID action)
        {
            this.position = position;
            this.action = action;
        }
    }
    
    public float stepTime = 2.0f;
    public int radius = 10;
    public float xUnit = 1.0f;
    public float yUnit = 1.0f;
    public float zUnit = 1.0f;
    public RuleSet ruleSet;
    /// <summary>
    /// The created grid will be this value in each direction.
    /// I.E. if the value is 10, then the grid of cells will be 10x10x10.
    /// </summary>
    public int gridSize;

    public double minFillPercentage;
    public double maxFillPercentage;
    
    private GameObject _cellsWithin;
    private CellularAutomata _cellularAutomata;
    public delegate void CellRuleSimple(byte neighborCount);
    public bool simpleCellRule = false;

    private const bool DebugMode = false;

    public GameObject cellPrefab;
    
    private ConcurrentDictionary<Vector3, bool> _interestingCells = new ConcurrentDictionary<Vector3, bool>();
    private ConcurrentDictionary<Vector3, GameObject> _cells = new ConcurrentDictionary<Vector3, GameObject>();

    private ConcurrentBag<Action> actionList;

    private void Start()
    {
        //The "Space" object has a "Volume" component and a "CellularAutomata" component
        _cellsWithin = GameObject.Find("Space");
        _cellularAutomata = _cellsWithin.GetComponent<CellularAutomata>();

        var rand = new System.Random();

        double fillPercent = 0;


        while (fillPercent < minFillPercentage || fillPercent > maxFillPercentage)
        {
            fillPercent = rand.NextDouble();
        }

        int cellCountToSpawn = CalculateFill(fillPercent);

        for (int i = 0; i < cellCountToSpawn; i++)
        {
            // Spawns cells randomly inside given grid area
            int randX = rand.Next(0, gridSize);
            int randY = rand.Next(0, gridSize);
            int randZ = rand.Next(0, gridSize);
            AddCell(new Vector3(randX, randY, randZ));
        }
    }

    /// <summary>
    /// Given a percentage that you want to fill the volume, returns the number of cells required
    /// </summary>
    private int CalculateFill(double percentToFill)
    {
        double totalSize = Math.Pow(gridSize, 3);

        return (int)(totalSize * percentToFill);
    }

    // TODO look at enabling / disabling cells rather than create / destroy, or pool instancing.
    private void AddCell(Vector3 position)
    {
        var cellObj = GameObject.Instantiate(cellPrefab,
            position,
            Quaternion.identity,
            // We instantiate versions of this object. This is more efficient
            // than creating entirely new object, especially since the prefab should have GPU instancing enabled.
            GameObject.FindGameObjectWithTag("Space").transform); 
        
        //Set the object as active; no point in creating the object if the playing ain't gonna be able to see it.
        cellObj.SetActive(true);
        
        _interestingCells.AddOrUpdate(position, // We record the position of this cell in the _interestingCells dictionary,
                            true, // and set the value to "true", meaning that there is an active cell at that position.
                    (oldKey, oldValue) => { return oldValue;}); // If for whatever reason we're unable to do that, just use the old value.
        _cells.AddOrUpdate(position, cellObj, (oldKey, oldVal) => oldVal); // Store the actual cell in a seperate dictionary, also using the position as the key.
        
        for (float x = position.x - xUnit; x <= position.x + 1; x += xUnit)
        {
            for (float y = position.y - yUnit; y <= position.y + 1; y += yUnit)
            {
                for (float z = position.z - zUnit; z <= position.z + 1; z += zUnit)
                {
                    if (x != position.x && y != position.y && z != position.z)
                    { 
                        //We need to add every cell that is adjacent to the newly created cell to the _interestingCells dictionary.
                        //Cells that are not bordering any active cells are effectively useless to us, 
                        //so all we care about are the active cells and all of the cells immediately surrounding active cells.
                        _interestingCells.AddOrUpdate(new Vector3(x,y,z),
                            false, //this one is just an interesting cell.
                            (oldKey, oldValue) => { return oldValue;});
                    }
                }
            }
        }
    }

    private void RemoveCell(Vector3 position)
    {
        // Stores the instanced game object.
        // We set that position to inactive in the _interestingCells dictionary
        // We still want to consider this cell for the future, because it may be bordering some other cell that will later become active,
        // meaning this cell may need to be referenced again.
        _interestingCells.AddOrUpdate(position, false, (oldKey, oldVal) => oldVal); 

        //Remove the game object, then destroy it.
        _cells.TryRemove(position, out var gameObj); 
        gameObj.Destroy();
    }
    
    private CellAction EvaluatePoint(Vector3 position)
    {
        byte neighborCount = 0;
        int  neighbors = 0;
        /*
         A cell has 26 possible neighbors; All we care about is whether or not an adjacent cell is occupied.
        So, we are going to store that in an int and use only 26 of the bits, where each adjacent cell is represented by a bit,
        with 0 meaning that the cell is empty, and 1 meaning that it is occupied.
        
        A cell's neighbors are organized into three layers:
                                         TOP3      MID3      BTM3
                        (1,1,1)------>  X X X    X X X    X X X  
                                        X X X    X O X    X X X
                                        X X X    X X X    X X X (-1,-1,-1) <-------
                ... where X's are adjacent to the cell of interest O                                    
        We store that as this:
                TOP3:          MID3:           BTM3: 
                   top mid btm    top ... ...   ... ... ...
                   XXX XXX XXX    XXX XOX XXX   XXX XXX XXX
                   [0, 1, 2, ...               ..., 24, 25]
        */
        bool isCellOccupied = _cells.ContainsKey(position);

        for (float x = position.x - xUnit; x <= position.x + xUnit; x++)
        {
            for (float y = position.x - yUnit; y <= position.y + yUnit; y++)
            {
                for (float z = position.z - zUnit; z <= position.z + zUnit; z++)
                {
                    var vec = new Vector3(x, y, z);
                    if(_interestingCells.ContainsKey(vec))
                    {
                        if (position != vec)
                        {
                            // set this cell's bit to 1;
                            neighbors++;
                            neighborCount++;
                        }
                    }
                    // shift bit to the left;
                    neighbors <<= 1; 
                }
            }
        }

        if (neighborCount == 0 && isCellOccupied == false)
        {
            return new CellAction(position, CellActionID.IgnorePos);
        }
        else
        {
            return new CellAction(position, ruleSet.CellRule(neighbors, neighborCount, isCellOccupied));
        }
    }

    public void DoStep()
    {
        // We create a list of actions that will accumulate as the cellular automata resolves the step.
        // We do this because we do not want the state of the volume to change while we're checking all of the cells.
        // if that happens, then when we run CellRule to determine what to do to each cell, changes made to other cells
        // will affect the behavior of different cells in the same step.
        
        actionList = new ConcurrentBag<Action>();
        List<Task> taskList = new List<Task>();


        //for every cell of interest
        foreach (KeyValuePair<Vector3, bool> cell in _interestingCells)
        {
            // Thanks SO https://stackoverflow.com/questions/49537761/cannot-convert-from-void-to-system-action
            Action mStep = () => MicroStep(cell);
            taskList.Add(Task.Factory.StartNew(mStep));
        }

        Task.WaitAll(taskList.ToArray());
        
        foreach (var action in actionList)
        {
            // Now we execute every action that the list accumulated.
            action();
        }
    }
    
    private void MicroStep(KeyValuePair<Vector3, bool> cell)
    {
        // We store the position
        Vector3 pos = cell.Key; 

        //evaluate the point
        CellAction response = EvaluatePoint(pos); 

        //then use the response to add an action to the list.
        switch (response.action) 
        {
            case CellActionID.Create:
                actionList.Add(() => { AddCell(pos);}); //Notice that this list takes an anonymous function as an element.
                if (true == DebugMode)
                {
                    Debug.Log($"Cell CREATED at {pos}.");
                }
                break;
            case CellActionID.Destroy:
                actionList.Add(() => { RemoveCell(pos);});
                if (true == DebugMode)
                {
                    Debug.Log($"Cell REMOVED at {pos}.");
                }
                break;
            case CellActionID.IgnorePos:
                bool dump;
                actionList.Add(() =>
                {
                    _interestingCells.TryRemove(pos,out dump);
                    GameObject gameObj;
                    _cells.TryRemove(pos, out gameObj);
                    gameObj.Destroy();
                });
                if (true == DebugMode)
                {
                    Debug.Log($"Cell IGNORED at {pos}.");
                }
                break;
            case CellActionID.Idle:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}