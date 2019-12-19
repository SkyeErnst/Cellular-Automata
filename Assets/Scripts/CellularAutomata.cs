using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class CellularAutomata : MonoBehaviour
{
    public Volume volume;
    public delegate void Task();
    public bool autoRun;

    private RuntimeRuleSet _rules;
    private float _frameTimeSum = 0.0f;
    
    private void Start()
    {
        _rules = GetComponent<RuntimeRuleSet>();
        if (null != _rules)
        {
            _rules.Setup();
        }
    }
    
    private void Update()
    {
        if (true == autoRun)
        {
            _frameTimeSum += Time.deltaTime;
            if (1.0f <= _frameTimeSum)
            {
                volume.DoStep();
                _frameTimeSum = 0.0f;
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                volume.DoStep();
            }
        }
    }
}

