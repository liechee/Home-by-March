using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace HomeByMarch
{
    public class HealthBar : MonoBehaviour
    {
        // //[SerializeField] private FloatEventChannel playerHealthChannel;
        // [SerializeField] private Image healthFill;
        // [SerializeField] private float changeSpeed = 2f; // Smooth transition speed
        // public float fillAmount = 1f;
        // public float FillAmount
        // {
        //     get => fillAmount;
        //     set => fillAmount = Mathf.Clamp01(value);
        // }


        // private void Update()
        // {
        //     // // Smoothly interpolate from current fill to the target fillAmount
        //     // healthFill.fillAmount = Mathf.Lerp(healthFill.fillAmount, fillAmount, changeSpeed * Time.deltaTime);
        //     // Debug.Log($"Interpolating healthFill.fillAmount: {healthFill.fillAmount}");
        //     if (Mathf.Abs(healthFill.fillAmount - fillAmount) > 0.001f)
        //     {
        //         healthFill.fillAmount = Mathf.Lerp(healthFill.fillAmount, fillAmount, changeSpeed * Time.deltaTime);
        //         Debug.Log($"Interpolating healthFill.fillAmount: {healthFill.fillAmount}");
        //     }
        //     else
        //     {
        //         healthFill.fillAmount = fillAmount; // Snap to final value
        //     }
        // }

        // // public void UpdateHealthBar(float healthPercentage)
        // // {
        // //     Debug.Log($"[HealthBarListener] Received health: {healthPercentage * 100}%");
        // //     fillAmount = Mathf.Clamp01(healthPercentage); // Just set the target, let Update handle the animation
        // //     Debug.Log($"Setting fillAmount to: {fillAmount}");
        // // }
        // public void UpdateHealthBar(float healthPercentage)
        // {
        //     fillAmount = Mathf.Clamp01(healthPercentage);
        //     Debug.Log($"[HealthBarListener] Received health: {fillAmount * 100}%");
        //     StopAllCoroutines();
        //     StartCoroutine(AnimateHealthBar());
        // }

        // private IEnumerator AnimateHealthBar()
        // {
        //     while (Mathf.Abs(healthFill.fillAmount - fillAmount) > 0.001f)
        //     {
        //         healthFill.fillAmount = Mathf.Lerp(healthFill.fillAmount, fillAmount, changeSpeed * Time.deltaTime);
        //         yield return null;
        //     }

        //     healthFill.fillAmount = fillAmount;
        // }
        [SerializeField] private Image healthFill;
        [SerializeField] private float lerpDuration = 0.5f; // Duration in seconds

        private Coroutine animationCoroutine;

        public void UpdateHealthBar(float healthPercentage)
        {
            // Clamp to avoid values outside 0-1 range
            healthPercentage = Mathf.Clamp01(healthPercentage);
            Debug.Log($"[HealthBarListener] Received health: {healthPercentage * 100}%");

            // Avoid unnecessary coroutine if value hasn't changed
            if (Mathf.Approximately(healthFill.fillAmount, healthPercentage))
                return;

            if (animationCoroutine != null)
                StopCoroutine(animationCoroutine);

            animationCoroutine = StartCoroutine(AnimateHealthBar(healthFill.fillAmount, healthPercentage));
        }

        private IEnumerator AnimateHealthBar(float from, float to)
        {
            float elapsed = 0f;

            while (elapsed < lerpDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / lerpDuration);
                healthFill.fillAmount = Mathf.Lerp(from, to, t);
                yield return null;
            }

            healthFill.fillAmount = Mathf.Clamp01(to); // Ensure it ends correctly
            Debug.Log($"[HealthBarListener] Final fill amount set to: {healthFill.fillAmount * 100}%");
        }

    }
}
