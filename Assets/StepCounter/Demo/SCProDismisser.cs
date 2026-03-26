using UnityEngine;
using UnityEngine.EventSystems;

public class SCProDismisser : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        gameObject.transform.parent.gameObject.SetActive(false);
    }
}
