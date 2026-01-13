using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ClaimQuestButton : MonoBehaviour, IPointerClickHandler{
// needed to click on the thing and then the quest shows up
// can edit to have actual assets and formatting but for now this will do
    private UnityAction onPointerClickAction;
    public Button button {get; private set;}
    private TMP_Text buttonText;
    public void Initialize(string displayName, UnityAction pointerClickAction){
        this.button = this.GetComponent<Button>();
        this.onPointerClickAction = pointerClickAction;
        this.buttonText = this.GetComponentInChildren<TMP_Text>();
        this.buttonText.text = "Claim !!";
        
    }


    public void OnPointerClick(PointerEventData pointerEventData){
        onPointerClickAction();
    }




}