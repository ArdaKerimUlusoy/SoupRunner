using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class SoupPhysics : MonoBehaviour
{
    [Header("References")]
    public Transform potTransform;
    public Transform soupLiquid;
    public ParticleSystem spillParticles;

    [Header("Soup Settings")]
    [Range(0f, 100f)]
    public float currentSoup = 100f;
    public float maxSoup = 100f;

    [Header("Balance Controls")]
    public float mouseSensitivityX = 0.22f;
    public float mouseSensitivityY = 0.20f;
    public float maxTiltRoll = 36f;
    public float maxTiltPitch = 28f;
    public float potCenteringSpeed = 3.5f;
    public bool requireMouseHold = false;
    public bool enableTightGrip = true;
    public bool invertMouseX = false;
    public bool invertMouseY = false;
    public bool autoLockCursor = true;

    [Header("Spill & Physics")]
    public float spillAngleThreshold = 18f;
    public float jumpTakeoffSpill = 2f;

    [Header("Pot Height Limits")]
    public float fullSoupHeight = 0.23f;
    public float emptySoupHeight = 0.02f;

    [Header("Inertia & Forces")]
    public float inertiaResponsiveness = 8.0f;
    public float runningGaitSensitivity = 0.65f;
    public float landingImpactSensitivity = 0.9f;

    private Quaternion initialPotLocalRotation = Quaternion.identity;
    private Vector3 initialPotLocalPosition = Vector3.zero;

    private float currentPotRoll = 0f;
    private float currentPotPitch = 0f;

    private float targetInertialRoll = 0f;
    private float currentInertialRoll = 0f;
    private float targetInertialPitch = 0f;
    private float currentInertialPitch = 0f;

    private float footstepGaitRoll = 0f;
    private float footstepGaitPitch = 0f;
    private float landingJoltPitch = 0f;
    private float landingJoltRoll = 0f;
    private float landingOffsetZ = 0f;

    private Vector3 currentLiquidAngle = Vector3.zero;

    private bool isBracing = false;
    private bool wasBracingLastFrame = false;
    private bool isCurrentlySpilling = false;
    private bool isGameOver = false;

    private bool hasLid = false;
    private float lidTimer = 0f;

    private float perfectStreakTimer = 0f;
    private bool wasInDangerZone = false;

    private void Awake()
    {
        if (SoupAudio.Instance == null)
        {
            GameObject audioObj = new GameObject("SoupAudio", typeof(SoupAudio));
            DontDestroyOnLoad(audioObj);
        }
    }

    private void Start()
    {
        if (potTransform == null && soupLiquid != null && soupLiquid.parent != null)
        {
            potTransform = soupLiquid.parent;
        }

        if (potTransform != null)
        {
            initialPotLocalRotation = potTransform.localRotation;
            initialPotLocalPosition = potTransform.localPosition;
        }

        SetupSpillParticles();

        if (autoLockCursor)
        {
            LockCursor(true);
        }

        UpdateLiquidLevel();
    }

    private void SetupSpillParticles()
    {
        if (spillParticles == null && potTransform != null)
        {
            spillParticles = potTransform.GetComponentInChildren<ParticleSystem>();
        }

        if (spillParticles != null)
        {
            var main = spillParticles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.gravityModifier = 1.6f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.08f, 0.95f), new Color(1f, 0.8f, 0.15f, 0.95f));

            var emission = spillParticles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[0]);

            var shape = spillParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.28f;

            ParticleSystemRenderer psr = spillParticles.GetComponent<ParticleSystemRenderer>();
            if (psr != null && soupLiquid != null)
            {
                Renderer liquidRend = soupLiquid.GetComponent<Renderer>();
                if (liquidRend != null && liquidRend.sharedMaterial != null)
                {
                    psr.sharedMaterial = liquidRend.sharedMaterial;
                }
            }

            spillParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void Update()
    {
        if (isGameOver) return;

        UpdateLidTimer();
        HandleCursorToggle();
        HandleManualPotControl();
        UpdateInertiaAndShockPhysics();
        ApplyCombinedPotTransform();
        ApplyLiquidPhysics();
        UpdateLiquidLevel();
        UpdateStreaksAndAlerts();

        if (SoupAudio.Instance != null)
        {
            SoupAudio.Instance.SetSpillSoundActive(isCurrentlySpilling);
        }
    }

    private void UpdateLidTimer()
    {
        if (hasLid)
        {
            lidTimer -= Time.deltaTime;
            if (lidTimer <= 0f)
            {
                hasLid = false;
                if (GameUIManager.Instance != null) GameUIManager.Instance.ShowFloatingText("LID REMOVED!", Color.white);
            }
        }
    }

    private void HandleCursorToggle()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            LockCursor(Cursor.lockState != CursorLockMode.Locked);
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor(true);
        }
    }

    public void LockCursor(bool locked)
    {
        if (locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void HandleManualPotControl()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || potTransform == null) return;

        isBracing = enableTightGrip && mouse.leftButton.isPressed;

        if (isBracing && !wasBracingLastFrame)
        {
            if (SoupAudio.Instance != null) SoupAudio.Instance.PlayBrace();
        }
        wasBracingLastFrame = isBracing;

        Vector2 mouseDelta = mouse.delta.ReadValue();

        float sensX = mouseSensitivityX * (invertMouseX ? 1f : -1f);
        float sensY = mouseSensitivityY * (invertMouseY ? -1f : 1f);

        currentPotRoll -= mouseDelta.x * sensX;
        currentPotPitch += mouseDelta.y * sensY;

        float centerSpeed = isBracing ? (potCenteringSpeed * 4.0f) : potCenteringSpeed;
        currentPotRoll = Mathf.Lerp(currentPotRoll, 0f, Time.deltaTime * centerSpeed);
        currentPotPitch = Mathf.Lerp(currentPotPitch, 0f, Time.deltaTime * centerSpeed);

        currentPotRoll = Mathf.Clamp(currentPotRoll, -maxTiltRoll, maxTiltRoll);
        currentPotPitch = Mathf.Clamp(currentPotPitch, -maxTiltPitch, maxTiltPitch);
    }

    public void SetRunnerInputTorques(float sustainedRoll, float sustainedPitch)
    {
        targetInertialRoll = sustainedRoll;
        targetInertialPitch = sustainedPitch;
    }

    public void ApplyRunningBobbing(float rollBob, float pitchBob)
    {
        footstepGaitRoll = rollBob * runningGaitSensitivity;
        footstepGaitPitch = pitchBob * runningGaitSensitivity;
    }

    public void OnJumpTakeoff()
    {
        if (currentSoup <= 0f) return;

        if (!hasLid)
        {
            ApplySoupDamage(jumpTakeoffSpill);
            TriggerSplashBurst(3);
        }

        targetInertialPitch += -6f;
        targetInertialRoll += Random.Range(-3f, 3f);

        if (SoupAudio.Instance != null) SoupAudio.Instance.PlayJump();
    }

    public void OnGroundLanding(float landingFallSpeed)
    {
        if (currentSoup <= 0f) return;

        float impact = Mathf.Clamp(landingFallSpeed * landingImpactSensitivity * 0.9f, 2f, 14f);

        landingJoltPitch = impact * 0.45f;
        landingJoltRoll = Random.Range(-impact * 0.2f, impact * 0.2f);
        landingOffsetZ = -0.02f * Mathf.Clamp01(landingFallSpeed / 15f);

        if (SoupAudio.Instance != null) SoupAudio.Instance.PlayLand();

        float netTilt = Mathf.Sqrt(currentPotRoll * currentPotRoll + currentPotPitch * currentPotPitch);
        if (netTilt > spillAngleThreshold && !hasLid)
        {
            ApplySoupDamage(impact * 0.15f);
            TriggerSplashBurst(4);
        }
    }

    public void OnHitObstacle(float damage = 20f, float pushDirection = 1f)
    {
        if (!hasLid)
        {
            ApplySoupDamage(damage);
        }

        targetInertialRoll += pushDirection * 18f;
        targetInertialPitch += Random.Range(-10f, 12f);
        TriggerSplashBurst(5);

        if (SoupAudio.Instance != null) SoupAudio.Instance.PlayHit();
    }

    private void UpdateInertiaAndShockPhysics()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float braceFactor = isBracing ? 0.15f : 1.0f;

        float desiredRoll = targetInertialRoll * braceFactor;
        float desiredPitch = targetInertialPitch * braceFactor;

        currentInertialRoll = Mathf.Lerp(currentInertialRoll, desiredRoll, dt * inertiaResponsiveness);
        currentInertialPitch = Mathf.Lerp(currentInertialPitch, desiredPitch, dt * inertiaResponsiveness);

        landingJoltPitch = Mathf.Lerp(landingJoltPitch, 0f, dt * 10f);
        landingJoltRoll = Mathf.Lerp(landingJoltRoll, 0f, dt * 10f);
        landingOffsetZ = Mathf.Lerp(landingOffsetZ, 0f, dt * 8f);
    }

    private void ApplyCombinedPotTransform()
    {
        if (potTransform == null) return;

        float netRoll = currentPotRoll + currentInertialRoll + footstepGaitRoll + landingJoltRoll;
        float netPitch = currentPotPitch + currentInertialPitch + footstepGaitPitch + landingJoltPitch;

        netRoll = Mathf.Clamp(netRoll, -45f, 45f);
        netPitch = Mathf.Clamp(netPitch, -35f, 35f);

        Quaternion tiltRotation = Quaternion.Euler(netPitch, 0f, netRoll);
        potTransform.localRotation = initialPotLocalRotation * tiltRotation;

        Vector3 dynamicOffset = new Vector3(0f, -landingJoltPitch * 0.002f, landingOffsetZ);
        potTransform.localPosition = initialPotLocalPosition + dynamicOffset;
    }

    private void ApplyLiquidPhysics()
    {
        if (soupLiquid == null || currentSoup <= 0f) return;

        float netRoll = currentPotRoll + currentInertialRoll + footstepGaitRoll + landingJoltRoll;
        float netPitch = currentPotPitch + currentInertialPitch + footstepGaitPitch + landingJoltPitch;

        Vector3 targetLiquidAngle = new Vector3(-netPitch, 0f, -netRoll);
        currentLiquidAngle = Vector3.Lerp(currentLiquidAngle, targetLiquidAngle, Time.deltaTime * 12f);
        soupLiquid.localRotation = Quaternion.Euler(currentLiquidAngle);

        float totalTilt = Mathf.Sqrt(netRoll * netRoll + netPitch * netPitch);

        if (totalTilt > spillAngleThreshold && !hasLid)
        {
            isCurrentlySpilling = true;
            float excess = totalTilt - spillAngleThreshold;
            float damage = Mathf.Clamp(excess * 1.6f, 2.5f, 15f) * Time.deltaTime;
            ApplySoupDamage(damage);

            if (spillParticles != null)
            {
                if (!spillParticles.isPlaying) spillParticles.Play();
                var emission = spillParticles.emission;
                emission.rateOverTime = Mathf.Clamp(excess * 4.5f, 16f, 38f);
            }
        }
        else
        {
            isCurrentlySpilling = false;
            if (spillParticles != null)
            {
                var emission = spillParticles.emission;
                emission.rateOverTime = 0f;
                if (spillParticles.isPlaying) spillParticles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    private void TriggerSplashBurst(int count)
    {
        if (spillParticles != null)
        {
            spillParticles.Emit(count);
        }
    }

    private void UpdateStreaksAndAlerts()
    {
        float tiltMagnitude = GetNetTilt().magnitude;
        float ratio = tiltMagnitude / spillAngleThreshold;

        if (ratio > 0.95f)
        {
            wasInDangerZone = true;
            perfectStreakTimer = 0f;
        }
        else if (ratio < 0.50f)
        {
            if (wasInDangerZone)
            {
                wasInDangerZone = false;
                if (GameUIManager.Instance != null) GameUIManager.Instance.ShowFloatingText("SAVED IT!", new Color(0.2f, 1f, 0.4f));
            }

            perfectStreakTimer += Time.deltaTime;
            if (perfectStreakTimer > 4.5f)
            {
                perfectStreakTimer = 0f;
                if (GameUIManager.Instance != null) GameUIManager.Instance.ShowFloatingText("PERFECT BALANCE!", new Color(0.1f, 0.9f, 1f));
            }
        }

        if (currentSoup < 25f && SoupAudio.Instance != null)
        {
            SoupAudio.Instance.PlayAlarm();
        }
    }

    public void RefillSoup(float amount)
    {
        currentSoup = Mathf.Clamp(currentSoup + amount, 0f, maxSoup);
        UpdateLiquidLevel();
    }

    public void ActivateLid(float duration)
    {
        hasLid = true;
        lidTimer = Mathf.Max(lidTimer, duration);
    }

    public void ApplySoupDamage(float amount)
    {
        if (currentSoup <= 0f || hasLid) return;

        currentSoup = Mathf.Clamp(currentSoup - amount, 0f, maxSoup);

        if (currentSoup <= 0f)
        {
            OnSoupEmpty();
        }
    }

    private void UpdateLiquidLevel()
    {
        if (soupLiquid == null) return;

        float ratio = currentSoup / maxSoup;
        float targetY = Mathf.Lerp(emptySoupHeight, fullSoupHeight, ratio);

        Vector3 p = soupLiquid.localPosition;
        soupLiquid.localPosition = new Vector3(p.x, targetY, p.z);
        soupLiquid.gameObject.SetActive(currentSoup > 0f);
    }

    private void OnSoupEmpty()
    {
        if (isGameOver) return;
        isGameOver = true;

        LockCursor(false);

        if (spillParticles != null)
        {
            var emission = spillParticles.emission;
            emission.rateOverTime = 0f;
            spillParticles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (SoupAudio.Instance != null)
        {
            SoupAudio.Instance.PlayGameOver();
        }

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.TriggerGameOver();
        }
    }

    public Vector2 GetNetTilt()
    {
        float netRoll = currentPotRoll + currentInertialRoll + footstepGaitRoll + landingJoltRoll;
        float netPitch = currentPotPitch + currentInertialPitch + footstepGaitPitch + landingJoltPitch;
        return new Vector2(netRoll, netPitch);
    }

    public float GetSpillThreshold() => spillAngleThreshold;
    public bool IsCurrentlySpilling() => isCurrentlySpilling;
    public bool IsBracing() => isBracing;
    public bool HasLid() => hasLid;
    public float GetLidRemainingTime() => lidTimer;
    public float GetSoupRatio() => currentSoup / maxSoup;
    public Vector2 GetRunnerInertiaVector() => new Vector2(currentInertialRoll, currentInertialPitch);
}
