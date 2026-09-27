using UnityEngine;

public class ObstaclePendulum : MonoBehaviour
{
    [Header("Sallanma Ayarlarý")]
    public float swingAngle = 60f;     
    public float swingSpeed = 2.5f;     
    public float randomOffset = 0f;   

    private Quaternion initialRotation;

    private void Start()
    {
        initialRotation = transform.localRotation;

        if (randomOffset == 0f)
        {
            randomOffset = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    private void Update()
    {
        float angle = Mathf.Sin((Time.time * swingSpeed) + randomOffset) * swingAngle;
        transform.localRotation = initialRotation * Quaternion.Euler(0f, 0f, angle);
    }
}