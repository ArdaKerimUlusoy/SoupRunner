using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CharacterController))]
public class SimpleRunnerController : MonoBehaviour
{
    [Header("Runner Controls")]
    public float strafeSpeed = 10.5f;

    [Header("Passive Speed Escalation")]
    public float baseWalkSpeed = 8.5f;
    public float maxWalkSpeed = 34f;
    public float passiveSpeedRamp = 0.038f;
    public float wSprintBoostMultiplier = 1.25f;
    public float brakeMultiplier = 0.60f;

    [Header("Counter Torque Tuning")]
    public float minLateralTorque = 24.0f;
    public float maxLateralTorque = 28.5f;
    public float minForwardTorque = 14.0f;
    public float maxForwardTorque = 21.0f;
    public float minBrakeTorque = 16.0f;
    public float maxBrakeTorque = 22.0f;

    [Header("Physics & Bounds")]
    public float jumpHeight = 1.8f;
    public float gravity = -22f;
    public float minX = -3.2f;
    public float maxX = 3.2f;

    [Header("Obstacle Damage")]
    public float baseObstacleDamage = 16f;
    public float hitCooldown = 0.5f;

    [Header("Effects")]
    public Camera playerCamera;
    public ParticleSystem speedWindFX;

    private CharacterController controller;
    private Vector3 verticalVelocity = Vector3.zero;
    private SoupPhysics soupPhysics;
    private float lastHitTime = -1f;
    private float startZ;
    private float currentEffectiveSpeed = 0f;
    private Vector3 camOriginalLocalPos;
    private Quaternion camOriginalLocalRot;
    private float defaultFOV = 60f;

    private bool wasGroundedLastFrame = true;
    private float footstepTimer = 0f;
    private float cameraLandingJolt = 0f;

    private bool isSpicyTurbo = false;
    private float spicyTurboTimer = 0f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        soupPhysics = GetComponent<SoupPhysics>();

        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera != null)
        {
            defaultFOV = playerCamera.fieldOfView;
            camOriginalLocalPos = playerCamera.transform.localPosition;
            camOriginalLocalRot = playerCamera.transform.localRotation;
        }
    }

    private void Start()
    {
        startZ = transform.position.z;
        wasGroundedLastFrame = controller.isGrounded;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.rKey.wasPressedThisFrame)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        UpdateTurboTimer();

        bool isGrounded = controller.isGrounded;
        if (isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2f;
        }

        float currentDistance = Mathf.Max(0f, transform.position.z - startZ);
        float passiveTrackSpeed = Mathf.Min(baseWalkSpeed + (currentDistance * passiveSpeedRamp), maxWalkSpeed);

        float inputX = 0f;
        float inputZ = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) inputX -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) inputX += 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) inputZ += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) inputZ -= 1f;

        bool isShiftPressed = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

        float targetForwardSpeed = passiveTrackSpeed;

        if (inputZ > 0.1f || isShiftPressed)
        {
            targetForwardSpeed *= wSprintBoostMultiplier;
        }
        else if (inputZ < -0.1f)
        {
            targetForwardSpeed *= brakeMultiplier;
        }

        if (isSpicyTurbo)
        {
            targetForwardSpeed *= 1.30f;
        }

        targetForwardSpeed = Mathf.Min(targetForwardSpeed, maxWalkSpeed * 1.15f);

        float currentXVelocity = inputX * strafeSpeed;
        Vector3 move = new Vector3(currentXVelocity, 0f, targetForwardSpeed);
        currentEffectiveSpeed = move.magnitude;

        if (soupPhysics != null)
        {
            float speedT = Mathf.Clamp01((currentEffectiveSpeed - baseWalkSpeed) / (maxWalkSpeed - baseWalkSpeed));

            float curLateralAngle = Mathf.Lerp(minLateralTorque, maxLateralTorque, speedT);
            float curForwardAngle = Mathf.Lerp(minForwardTorque, maxForwardTorque, speedT);
            float curBrakeAngle = Mathf.Lerp(minBrakeTorque, maxBrakeTorque, speedT);

            float sustainedRoll = -inputX * curLateralAngle;

            float sustainedPitch = 0f;
            if (inputZ > 0.1f || isShiftPressed)
            {
                sustainedPitch = -curForwardAngle;
            }
            else if (inputZ < -0.1f)
            {
                sustainedPitch = curBrakeAngle;
            }

            soupPhysics.SetRunnerInputTorques(sustainedRoll, sustainedPitch);

            if (isGrounded && currentEffectiveSpeed > 1f)
            {
                footstepTimer += Time.deltaTime * currentEffectiveSpeed * 0.65f;
                float gaitIntensity = Mathf.Lerp(1.2f, 2.8f, speedT);
                float bobRoll = Mathf.Sin(footstepTimer) * gaitIntensity;
                float bobPitch = Mathf.Abs(Mathf.Cos(footstepTimer)) * (gaitIntensity * 0.5f);
                soupPhysics.ApplyRunningBobbing(bobRoll, bobPitch);

                float stepInterval = Mathf.Lerp(0.38f, 0.20f, speedT);
                if (SoupAudio.Instance != null)
                {
                    SoupAudio.Instance.PlayFootstep(speedT > 0.45f, stepInterval);
                }
            }
            else
            {
                soupPhysics.ApplyRunningBobbing(0f, 0f);
            }
        }

        if (keyboard.spaceKey.wasPressedThisFrame && isGrounded)
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

        if (!wasGroundedLastFrame && controller.isGrounded && verticalVelocity.y < 0f)
        {
            float impactFallSpeed = Mathf.Abs(verticalVelocity.y);
            if (soupPhysics != null)
            {
                soupPhysics.OnGroundLanding(impactFallSpeed);
            }
            cameraLandingJolt = Mathf.Clamp(impactFallSpeed * 0.012f, 0.02f, 0.08f);
        }
        wasGroundedLastFrame = controller.isGrounded;

        Vector3 clampedPos = transform.position;
        if (clampedPos.x < minX || clampedPos.x > maxX)
        {
            clampedPos.x = Mathf.Clamp(clampedPos.x, minX, maxX);
            transform.position = clampedPos;
        }

        UpdateCameraAndVisualEffects(currentEffectiveSpeed / maxWalkSpeed, inputX);
    }

    public void TriggerSpicyTurbo(float duration)
    {
        isSpicyTurbo = true;
        spicyTurboTimer = Mathf.Max(spicyTurboTimer, duration);
        if (SoupAudio.Instance != null) SoupAudio.Instance.PlayWhoosh();
    }

    private void UpdateTurboTimer()
    {
        if (isSpicyTurbo)
        {
            spicyTurboTimer -= Time.deltaTime;
            if (spicyTurboTimer <= 0f)
            {
                isSpicyTurbo = false;
            }
        }
    }

    public bool IsSpicyTurboActive() => isSpicyTurbo;
    public float GetSpicyTurboRemainingTime() => spicyTurboTimer;

    private void UpdateCameraAndVisualEffects(float speedRatio, float inputX)
    {
        bool showWind = (speedRatio > 0.65f) || isSpicyTurbo;
        if (speedWindFX != null)
        {
            if (showWind && !speedWindFX.isPlaying)
            {
                speedWindFX.Play();
            }
            else if (!showWind && speedWindFX.isPlaying)
            {
                speedWindFX.Stop();
            }
        }

        if (playerCamera != null)
        {
            float targetFOV = isSpicyTurbo ? (defaultFOV + 14f) : (defaultFOV + (speedRatio * 10f));
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, Time.deltaTime * 5f);

            cameraLandingJolt = Mathf.Lerp(cameraLandingJolt, 0f, Time.deltaTime * 12f);
            Vector3 camPos = camOriginalLocalPos + new Vector3(0f, -cameraLandingJolt, 0f);
            playerCamera.transform.localPosition = camPos;

            float targetCamRoll = -inputX * 1.5f;
            Quaternion targetRot = camOriginalLocalRot * Quaternion.Euler(0f, 0f, targetCamRoll);
            playerCamera.transform.localRotation = Quaternion.Slerp(playerCamera.transform.localRotation, targetRot, Time.deltaTime * 8f);
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
                          hitObject.name.Contains("spikewall") ||
                          hitObject.name.Contains("blade") ||
                          hitObject.name.Contains("log") ||
                          hitObject.name.Contains("Root") ||
                          hitObject.name.Contains("root");

        if (isObstacle)
        {
            lastHitTime = Time.time;

            float speedFactor = Mathf.Clamp(currentEffectiveSpeed / baseWalkSpeed, 0.9f, 2.5f);
            float finalDamage = baseObstacleDamage * speedFactor;
            float pushDir = (transform.position.x < hitObject.transform.position.x) ? -1f : 1f;

            if (soupPhysics != null)
            {
                soupPhysics.OnHitObstacle(finalDamage, pushDir);
            }

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
                playerCamera.transform.localPosition = camOriginalLocalPos + new Vector3(shake, Random.Range(-0.03f, 0.03f) * intensity, 0f);
            }

            yield return null;
        }

        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = camOriginalLocalPos;
        }
    }
}
