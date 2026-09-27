using UnityEngine;

public enum PickupType
{
    SoupRefill,
    SpicyChili,
    GoldenLid
}

public class SoupPickup : MonoBehaviour
{
    public PickupType pickupType;
    public float magnetDistance = 4.5f;
    public float magnetSpeed = 14f;

    private Transform visualChild;
    private Transform playerTransform;
    private Vector3 initialLocalPos;
    private float floatPhase;
    private bool isCollected = false;

    private void Start()
    {
        floatPhase = Random.Range(0f, Mathf.PI * 2f);
        initialLocalPos = transform.position;

        CreateVisualsIfNeeded();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }

        SphereCollider col = GetComponent<SphereCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<SphereCollider>();
        }
        col.isTrigger = true;
        col.radius = 0.9f;
    }

    private void CreateVisualsIfNeeded()
    {
        if (transform.childCount > 0)
        {
            visualChild = transform.GetChild(0);
            return;
        }

        GameObject visual = null;
        Color visualColor = Color.yellow;

        switch (pickupType)
        {
            case PickupType.SoupRefill:
                visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                visual.transform.localScale = new Vector3(0.7f, 0.25f, 0.7f);
                visualColor = new Color(1f, 0.6f, 0.1f, 1f);
                break;
            case PickupType.SpicyChili:
                visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.transform.localScale = new Vector3(0.35f, 0.6f, 0.35f);
                visual.transform.localRotation = Quaternion.Euler(25f, 0f, 25f);
                visualColor = new Color(1f, 0.15f, 0.1f, 1f);
                break;
            case PickupType.GoldenLid:
                visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                visual.transform.localScale = new Vector3(0.9f, 0.1f, 0.9f);
                visualColor = new Color(1f, 0.85f, 0.15f, 1f);
                break;
        }

        if (visual != null)
        {
            visual.name = "PickupVisual";
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = Vector3.zero;

            Collider c = visual.GetComponent<Collider>();
            if (c != null) Destroy(c);

            Renderer rend = visual.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = visualColor;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", visualColor * 0.7f);
                rend.material = mat;
            }

            visualChild = visual.transform;
        }
    }

    private void Update()
    {
        if (isCollected) return;

        if (visualChild != null)
        {
            float bob = Mathf.Sin((Time.time * 3.2f) + floatPhase) * 0.14f;
            visualChild.localPosition = new Vector3(0f, bob + 0.5f, 0f);
            visualChild.Rotate(0f, 110f * Time.deltaTime, 0f, Space.World);
        }

        if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist < magnetDistance)
            {
                transform.position = Vector3.MoveTowards(transform.position, playerTransform.position + Vector3.up * 0.5f, magnetSpeed * Time.deltaTime);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;

        SoupPhysics soup = other.GetComponentInParent<SoupPhysics>();
        SimpleRunnerController runner = other.GetComponentInParent<SimpleRunnerController>();

        if (soup != null || runner != null)
        {
            isCollected = true;
            ApplyPickupEffect(soup, runner);
            Destroy(gameObject);
        }
    }

    private void ApplyPickupEffect(SoupPhysics soup, SimpleRunnerController runner)
    {
        switch (pickupType)
        {
            case PickupType.SoupRefill:
                if (soup != null) soup.RefillSoup(25f);
                if (SoupAudio.Instance != null) SoupAudio.Instance.PlayPickupSoup();
                if (GameUIManager.Instance != null) GameUIManager.Instance.ShowFloatingText("+25% SOUP REFILL!", new Color(1f, 0.65f, 0.1f));
                break;

            case PickupType.SpicyChili:
                if (runner != null) runner.TriggerSpicyTurbo(4.5f);
                if (SoupAudio.Instance != null) SoupAudio.Instance.PlayPickupChili();
                if (GameUIManager.Instance != null) GameUIManager.Instance.ShowFloatingText("HOT CHILI TURBO!", new Color(1f, 0.2f, 0.1f));
                break;

            case PickupType.GoldenLid:
                if (soup != null) soup.ActivateLid(5f);
                if (SoupAudio.Instance != null) SoupAudio.Instance.PlayPickupLid();
                if (GameUIManager.Instance != null) GameUIManager.Instance.ShowFloatingText("POT LID SECURED!", new Color(1f, 0.9f, 0.2f));
                break;
        }
    }
}
