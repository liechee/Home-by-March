using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System;
using System.Collections;

public class ResultHandler : MonoBehaviour
{
    public GameObject resultPanel;
    public Image resultIcon;
    public TextMeshProUGUI resultCount;
    public GameObject confetti; // Add confetti object

    public SFXManager SFXManager;

    private void Awake()
    {
        // Initialization if needed
    }

    public void ShowResult(Sprite icon, string countText)
    {
        if (resultPanel)
        {
            resultPanel.SetActive(true);
            resultIcon.sprite = icon;
            resultCount.text = countText;
            resultPanel.GetComponent<Animator>().Play("clip");
        }
         // Play sound effect
        StartCoroutine(HideResultAfterDelay(3.3f));
        StartCoroutine(ShowConfettiAfterDelay(1.5f));
        StartCoroutine(PlaySFX(1f)); // Start coroutine to show confetti
    }

    private IEnumerator HideResultAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideResult();
    }
     private IEnumerator PlaySFX(float delay)
    {
        yield return new WaitForSeconds(delay);
        SFXManager.PlaySFX(SoundTypes.Rerwards, 0);
    }

    private IEnumerator ShowConfettiAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (confetti)
        {
            
            confetti.SetActive(true);
        }
    }

    public void HideResult()
    {
        if (resultPanel)
        {
            resultPanel.SetActive(false);
            confetti.SetActive(false);
        }
    }
}