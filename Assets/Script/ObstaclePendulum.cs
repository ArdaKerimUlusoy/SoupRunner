using UnityEngine;

public class ObstaclePendulum : MonoBehaviour
{
    [Header("Swing Settings")]
    public float swingAngle = 60f;     
    public float swingSpeed = 2.8f;     
    public float randomOffset = 0f;   
    public bool scaleWithDistance = true;

    private Quaternion initialRotation;
    private Transform playerTransform;
    private float startZ = 0f;

    private void Start()
    {
        initialRotation = transform.localRotation;

        if (randomOffset == 0f)
        {
            randomOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            playerTransform = p.transform;
            startZ = playerTransform.position.z;
        }
    }

    private void Update()
    {
        float currentSpeed = swingSpeed;
        if (scaleWithDistance && playerTransform != null)
        {
            float distance = Mathf.Max(0f, playerTransform.position.z - startZ);
            currentSpeed = Mathf.Min(swingSpeed + (distance * 0.003f), swingSpeed * 1.6f);
        }

        float angle = Mathf.Sin((Time.time * currentSpeed) + randomOffset) * swingAngle;
        transform.localRotation = initialRotation * Quaternion.Euler(0f, 0f, angle);
    }
}
