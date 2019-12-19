
using System;
using UnityEngine;

public class RuntimeRuleSet : RuleSet
{
    public uint idlePoint;
    public uint destroyPoint;
    public uint birthPoint;

    private bool _setupComplete = false;


    public RuntimeRuleSet(Volume volume) : base(volume)
    {
        this.volume = volume;
    }

    public void Setup()
    {
        _setupComplete = true;
    }
    
    public void Setup(uint idle, uint destroy, uint birth)
    {
        idlePoint = idle;
        destroyPoint = destroy;
        birthPoint = birth;
        _setupComplete = true;
    }

    public override Volume.CellActionID CellRule(int neighbors, byte neighborCount, bool isCellOccupied)
    {
        if (false == _setupComplete)
        {
            Debug.LogError("Custom rules not set up!");
            throw new Exception("Rule set Exception. Custom rule set not set up or is broken");
        }
        
        if (isCellOccupied)
        {
            if (neighborCount > destroyPoint)
            {
                return Volume.CellActionID.Destroy;
            }
            else if (neighborCount >= idlePoint)
            {
                return Volume.CellActionID.Idle;
            }
            else
            {
                return Volume.CellActionID.Destroy;
            }
        }
        else
        {
            if (neighborCount == birthPoint)
            {
                return Volume.CellActionID.Create;
            }
            else
            {
                return Volume.CellActionID.IgnorePos;
            }
        }
    }
}