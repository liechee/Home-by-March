using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HomeByMarch{


public interface IState
{
    // Start is called before the first frame update
    void OnEnter();
    void Update();
    void FixedUpdate();
    void OnExit();
} 
}

