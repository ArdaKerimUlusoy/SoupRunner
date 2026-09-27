using UnityEngine;
using UnityEngine.InputSystem;

public class SoupPhysics : MonoBehaviour
{
    [Header("Referanslar")]
    public Transform potTransform;         // Tencere objesi
    public Transform soupLiquid;          // Çorba diski
    public ParticleSystem spillParticles; // Fýþkýrma partikülü

    [Header("Çorba Deðerleri")]
    [Range(0f, 100f)]
    public float currentSoup = 100f;
    public float maxSoup = 100f;

    [Header("Mouse ile Denge Kontrolü")]
    public float mouseSensitivityX = 0.08f;
    public float mouseSensitivityY = 0.08f;
    public float maxTiltRoll = 35f;
    public float maxTiltPitch = 25f;
    public float panicWhipThreshold = 85f;

    [Header("Dökülme & Fizik Hassasiyeti")]
    public float spillAngleThreshold = 26f;
    public float spillRate = 18f;
    public float jumpTakeoffSpill = 4f;

    [Header("Yükseklik Sýnýrlarý (Tencere Ýçi)")]
    public float fullSoupHeight = 0.23f;   // %100 doluyken tencere aðzýna yakýn yükseklik
    public float emptySoupHeight = 0.02f;  // %0 iken tencere taban yüksekliði (eksiye düþürmüyoruz)

    private Vector3 lastPlayerPos;
    private float horizontalVelocity;
    private float verticalVelocity;

    private float currentPotRoll = 0f;
    private float currentPotPitch = 0f;

    private Vector3 currentLiquidAngle = Vector3.zero;
    private float lastParticleTime = 0f;
    private const float PARTICLE_COOLDOWN = 0.25f;

    private void Start()
    {
        lastPlayerPos = transform.position;

        if (potTransform == null && soupLiquid != null && soupLiquid.parent != null)
        {
            potTransform = soupLiquid.parent;
        }

        // Baþlangýç seviyesini doðrudan uygula
        UpdateLiquidLevel();
    }

    private void Update()
    {
        HandleManualPotControl();
        CalculateVelocities();
        ApplyLiquidPhysics();
        UpdateLiquidLevel();
    }

    private void HandleManualPotControl()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || potTransform == null) return;

        if (!mouse.leftButton.isPressed) return;

        Vector2 mouseDelta = mouse.delta.ReadValue();

        currentPotRoll -= mouseDelta.x * mouseSensitivityX;
        currentPotRoll = Mathf.Clamp(currentPotRoll, -maxTiltRoll, maxTiltRoll);

        currentPotPitch += mouseDelta.y * mouseSensitivityY;
        currentPotPitch = Mathf.Clamp(currentPotPitch, -maxTiltPitch, maxTiltPitch);

        potTransform.localRotation = Quaternion.Euler(currentPotPitch, 0f, currentPotRoll);

        if (Time.deltaTime > 0f)
        {
            float mouseSpeed = mouseDelta.magnitude / Time.deltaTime;
            if (mouseSpeed > panicWhipThreshold * 100f)
            {
                ApplySoupDamage(mouseSpeed * 0.0004f);
                TriggerSplashFX();
            }
        }
    }

    private void CalculateVelocities()
    {
        Vector3 currentPos = transform.position;
        if (Time.deltaTime > 0f)
        {
            horizontalVelocity = (currentPos.x - lastPlayerPos.x) / Time.deltaTime;
            verticalVelocity = (currentPos.y - lastPlayerPos.y) / Time.deltaTime;
        }

        lastPlayerPos = currentPos;
    }

    private void ApplyLiquidPhysics()
    {
        if (soupLiquid == null || currentSoup <= 0f) return;

        float bodyTiltRoll = Mathf.Clamp(horizontalVelocity * -3.2f, -38f, 38f);
        float bodyTiltPitch = Mathf.Clamp(verticalVelocity * -1.2f, -15f, 15f);

        float netRoll = bodyTiltRoll - currentPotRoll;
        float netPitch = bodyTiltPitch - currentPotPitch;

        Vector3 targetLiquidAngle = new Vector3(netPitch, 0f, netRoll);

        currentLiquidAngle = Vector3.Lerp(currentLiquidAngle, targetLiquidAngle, Time.deltaTime * 10f);
        currentLiquidAngle = Vector3.Lerp(currentLiquidAngle, Vector3.zero, Time.deltaTime * 4f);

        soupLiquid.localRotation = Quaternion.Euler(currentLiquidAngle);

        float totalTilt = Mathf.Sqrt(currentLiquidAngle.x * currentLiquidAngle.x + currentLiquidAngle.z * currentLiquidAngle.z);

        if (totalTilt > spillAngleThreshold)
        {
            float excess = totalTilt - spillAngleThreshold;
            float damage = excess * spillRate * 0.08f * Time.deltaTime;
            ApplySoupDamage(damage);
            TriggerSplashFX();
        }
    }

    public void OnJumpTakeoff()
    {
        if (currentSoup <= 0f) return;

        ApplySoupDamage(jumpTakeoffSpill);
        currentLiquidAngle = new Vector3(-12f, 0f, Random.Range(-6f, 6f));
        TriggerSplashFX();
    }

    private void TriggerSplashFX()
    {
        if (spillParticles != null && Time.time - lastParticleTime > PARTICLE_COOLDOWN)
        {
            spillParticles.Play();
            lastParticleTime = Time.time;
        }
    }

    public void ApplySoupDamage(float amount)
    {
        if (currentSoup <= 0f) return;

        currentSoup = Mathf.Clamp(currentSoup - amount, 0f, maxSoup);

        if (currentSoup <= 0f)
        {
            OnSoupEmpty();
        }
    }

    private void UpdateLiquidLevel()
    {
        if (soupLiquid == null) return;

        // Barda görünen oran ile tenceredeki oran tam 1:1 eþleþir
        float ratio = currentSoup / maxSoup;
        float targetY = Mathf.Lerp(emptySoupHeight, fullSoupHeight, ratio);

        Vector3 p = soupLiquid.localPosition;
        soupLiquid.localPosition = new Vector3(p.x, targetY, p.z);
        soupLiquid.gameObject.SetActive(currentSoup > 0f);
    }

    public void OnHitObstacle(float damage = 20f)
    {
        ApplySoupDamage(damage);
        currentLiquidAngle = new Vector3(Random.Range(-25f, 25f), 0f, Random.Range(-30f, 30f));
        TriggerSplashFX();
    }

    private void OnSoupEmpty()
    {
        Debug.Log("<color=red><b>GAME OVER!</b> Çorba tamamen döküldü!</color>");

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.TriggerGameOver();
        }
    }
}