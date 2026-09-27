using UnityEngine;

public class SoupWaveTexture : MonoBehaviour
{
    public float waveSpeedX = 0.15f;
    public float waveSpeedY = 0.12f;
    private Renderer rend;

    void Start()
    {
        rend = GetComponent<Renderer>();
    }

    void Update()
    {
        if (rend != null)
        {
            float offsetX = Time.time * waveSpeedX;
            float offsetY = Time.time * waveSpeedY;
            rend.material.mainTextureOffset = new Vector2(offsetX, offsetY);
        }
    }
}