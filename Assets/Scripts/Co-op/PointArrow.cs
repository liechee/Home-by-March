using UnityEngine;

public class PointArrow : MonoBehaviour
{
    [SerializeField] private Transform target; // The object the arrow will point at

    private void Update()
    {
        if (target != null)
        {
            // Calculate the direction to the target
            Vector3 direction = target.position - transform.position;
            
            // Normalize the direction to ensure it has a magnitude of 1
            direction.Normalize();

            // Calculate the rotation to look at the target
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // Apply the rotation to the arrow
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
