using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SimpleRunnerController : MonoBehaviour
{
    [Header("Hýz & Ýlerleme Ayarlarý")]
    public float baseWalkSpeed = 6.5f;       // Baþlangýç yürüme hýzý
    public float maxWalkSpeed = 15f;         // Mesafeyle ulaþýlabilecek tavan yürüme hýzý
    public float speedIncreaseRate = 0.005f;  // Metre baþýna hýzlanma katsayýsý
    public float sprintMultiplier = 1.45f;   // Shift depar çarpaný
    public float reverseSpeedMultiplier = 0.5f; // S tuþu yavaþlama/geri çarpaný

    [Header("Fizik & Sýnýrlar")]
    public float jumpHeight = 1.6f;
    public float gravity = -22f;
    public float minX = -3.2f;
    public float maxX = 3.2f;

    [Header("Dinamik Çarpýþma Hasarý")]
    public float baseObstacleDamage = 16f;   // Düþük hýzda çarpýnca gidecek taban çorba %'si
    public float hitCooldown = 0.5f;

    [Header("Efekt Referanslarý")]
    public Camera playerCamera;
    public ParticleSystem speedWindFX;

    private CharacterController controller;
    private Vector3 verticalVelocity = Vector3.zero;
    private SoupPhysics soupPhysics;
    private float lastHitTime = -1f;
    private float startZ;
    private float currentEffectiveSpeed = 0f;
    private Vector3 camOriginalLocalPos;
    private float defaultFOV = 60f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        soupPhysics = GetComponent<SoupPhysics>();

        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera != null)
        {
            defaultFOV = playerCamera.fieldOfView;
            camOriginalLocalPos = playerCamera.transform.localPosition;
        }
    }

    private void Start()
    {
        startZ = transform.position.z;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (controller.isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2f;
        }

        // 1. Mesafeye Göre Dinamik Hýz Tabaný
        float currentDistance = Mathf.Max(0f, transform.position.z - startZ);
        float distanceScaledSpeed = Mathf.Min(baseWalkSpeed + (currentDistance * speedIncreaseRate), maxWalkSpeed);

        // 2. Oyuncu Giriþleri (W ile ileri, S ile yavaþla/geri, A/D ile saða-sola)
        float moveX = 0f;
        float moveZ = 0f;

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveZ += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveZ -= 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveX -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveX += 1f;

        // Shift ile isteðe baðlý depar
        bool isSprinting = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

        float targetSpeed = distanceScaledSpeed;
        if (moveZ < 0f)
        {
            targetSpeed *= reverseSpeedMultiplier; // S'ye basýnca yavaþlar
        }
        else if (isSprinting && moveZ > 0f)
        {
            targetSpeed *= sprintMultiplier; // W + Shift basýnca depar atar
        }

        Vector3 moveInput = new Vector3(moveX, 0f, moveZ).normalized;
        Vector3 move = moveInput * targetSpeed;

        // Anlýk hýzý takip et (Hasar hesabý için)
        currentEffectiveSpeed = move.magnitude;

        // 3. Rüzgar Efekti & FOV Kontrolü (Hýzlý koþarken kenarlarda rüzgar aksýn)
        bool shouldShowWind = isSprinting && moveZ > 0f;
        if (speedWindFX != null)
        {
            if (shouldShowWind && !speedWindFX.isPlaying)
            {
                speedWindFX.Play();
            }
            else if (!shouldShowWind && speedWindFX.isPlaying)
            {
                speedWindFX.Stop();
            }
        }

        if (playerCamera != null)
        {
            float targetFOV = shouldShowWind ? (defaultFOV + 8f) : defaultFOV;
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, Time.deltaTime * 6f);
        }

        // 4. Zýplama
        if (keyboard.spaceKey.wasPressedThisFrame && controller.isGrounded)
        {
            verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (soupPhysics != null)
            {
                soupPhysics.OnJumpTakeoff();
            }
        }

        verticalVelocity.y += gravity * Time.deltaTime;

        Vector3 finalVelocity = (move + verticalVelocity) * Time.deltaTime;
        controller.Move(finalVelocity);

        // Þerit sýnýrlarý
        Vector3 clampedPos = transform.position;
        if (clampedPos.x < minX || clampedPos.x > maxX)
        {
            clampedPos.x = Mathf.Clamp(clampedPos.x, minX, maxX);
            transform.position = clampedPos;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckAndApplyDamage(other.gameObject);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.normal.y > 0.6f) return;
        CheckAndApplyDamage(hit.gameObject);
    }

    private void CheckAndApplyDamage(GameObject hitObject)
    {
        if (Time.time - lastHitTime < hitCooldown) return;

        bool isObstacle = hitObject.CompareTag("Obstacle") ||
                          hitObject.name.Contains("Obstacle") ||
                          hitObject.name.Contains("Barrier") ||
                          hitObject.name.Contains("Hammer") ||
                          hitObject.name.Contains("spikewall");

        if (isObstacle)
        {
            lastHitTime = Time.time;

            // Hýza Göre Dinamik Çorba Hasarý:
            // Yavaþ çarpýlýrsa (örneðin hýz 6 ise) ~16% çorba gider.
            // Shift + yüksek hýzda çarpýlýrsa (örneðin hýz 18 ise) ~40% ve üzeri çorba fýrlar.
            float speedFactor = Mathf.Clamp(currentEffectiveSpeed / baseWalkSpeed, 0.8f, 2.6f);
            float finalDamage = baseObstacleDamage * speedFactor;

            if (soupPhysics != null)
            {
                soupPhysics.OnHitObstacle(finalDamage);
            }

            // Savrulma ve kamera darbesi
            float pushDir = (transform.position.x < hitObject.transform.position.x) ? -1f : 1f;
            StopCoroutine("KnockbackRoutine");
            StartCoroutine(KnockbackRoutine(pushDir, speedFactor));
        }
    }

    private IEnumerator KnockbackRoutine(float directionX, float intensity)
    {
        float pushDuration = 0.22f;
        float pushSpeed = 10f * intensity;
        float elapsed = 0f;

        while (elapsed < pushDuration)
        {
            elapsed += Time.deltaTime;
            controller.Move(new Vector3(directionX * pushSpeed * Time.deltaTime, 0f, 0f));

            if (playerCamera != null)
            {
                float shake = Random.Range(-0.06f, 0.06f) * intensity;
                playerCamera.transform.localPosition = camOriginalLocalPos + new Vector3(shake, 0f, 0f);
            }

            yield return null;
        }

        if (playerCamera != null) playerCamera.transform.localPosition = camOriginalLocalPos;
    }
}