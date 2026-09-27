using UnityEngine;

public class SoupWaveTexture : MonoBehaviour
{
    public float waveSpeedX = 0.15f;
    public float waveSpeedY = 0.12f;

    private Renderer rend;
    private SimpleRunnerController runner;
    private SoupPhysics soupPhysics;

    private void Start()
    {
        rend = GetComponent<Renderer>();
        runner = GetComponentInParent<SimpleRunnerController>();
        soupPhysics = GetComponentInParent<SoupPhysics>();
    }

    private void Update()
    {
        if (rend != null)
        {
            float multiplier = 1f;
            if (runner != null && runner.IsSpicyTurboActive())
            {
                multiplier = 2.8f;
            }
            else if (soupPhysics != null && soupPhysics.IsCurrentlySpilling())
            {
                multiplier = 2.0f;
            }

            float offsetX = Time.time * waveSpeedX * multiplier;
            float offsetY = Time.time * waveSpeedY * multiplier;
            rend.material.mainTextureOffset = new Vector2(offsetX, offsetY);
        }
    }
}
