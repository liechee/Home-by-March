using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cutscene : MonoBehaviour
{
    public GameObject player;
    public GameObject cutsceneCamera;

    void OnTriggerEnter(Collider other){
        this.gameObject.SetActive(false);
        cutsceneCamera.SetActive(true);
        player.SetActive(false);
        StartCoroutine(endCutscene());
    }

    IEnumerator endCutscene(){ 
        yield return new WaitForSeconds(5);
        player.SetActive(true);
        cutsceneCamera.SetActive(false);
    }
}