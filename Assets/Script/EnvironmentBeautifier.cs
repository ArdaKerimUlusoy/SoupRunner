using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class EnvironmentBeautifier : MonoBehaviour
{
    [Header("Genel")]
    public string roadObjectName = "Road";
    public int seed = 1234;

    [Header("Atmosfer")]
    public bool applyFog = true;
    public Color fogColor = new Color(0.64f, 0.79f, 0.93f);
    public float fogStart = 35f;
    public float fogEnd = 175f;
    public bool adjustSun = true;
    public Color sunColor = new Color(1.00f, 0.95f, 0.85f);
    public bool applyPostProcessing = true;

    [Header("Yol")]
    public bool improveRoad = true;
    public Vector2 roadTiling = new Vector2(2f, 7f);
    public Color asphaltTint = new Color(0.92f, 0.92f, 0.95f);

    [Header("Şerit Çizgileri")]
    public bool laneMarkings = true;
    public float[] dashedLineX = { -1.2f, 1.2f };
    public bool edgeLines = true;
    public float edgeLineInset = 0.25f;
    public float dashLength = 3f;
    public float dashPeriod = 6f;
    public float lineWidth = 0.14f;
    public Color lineColor = new Color(0.96f, 0.96f, 0.92f);

    [Header("Çevre")]
    public bool grassGround = true;
    public float groundY = -0.25f;
    public float grassTileSize = 8f;
    public bool shoulders = true;
    public float shoulderWidth = 2.2f;
    public bool hideOldBuildings = true;
    public bool trees = true;
    [Range(0f, 2f)] public float treeDensity = 1f;
    public float farTreeDistance = 55f;
    public bool streetLamps = true;
    public float lampSpacing = 15f;
    public bool hills = true;

    // ------------------------------------------------------------------ internals
    Transform player, groundT;
    bool initialized;
    float scanTimer;
    int scanCount;
    readonly List<GameObject> roots = new List<GameObject>();
    readonly HashSet<Transform> processed = new HashSet<Transform>();

    float roadHalfW = 4.364f, roadTop = 0.1f, roadBottom = -0.1f;
    float barrierInner = 3.7f, barrierOuter = 4.4f;
    bool geometryMeasured;

    Material template;
    readonly List<Object> owned = new List<Object>();
    readonly Dictionary<Material, Material> roadMatMap = new Dictionary<Material, Material>();

    Material matGrass, matGravel, matLine, matBark, matMetal, matLampHead, matHill;
    Material[] matLeaves, matRocks;
    Material[][] treeMats, bushMats, rockMats;
    Material[] lampMats;

    Mesh quadMesh, shoulderL, shoulderR, lampMesh, hillMesh;
    Mesh[] pineMeshes, treeMeshes, bushMeshes, rockMeshes;

    // ================================================================== lifecycle
    void Start()
    {
        if (applyFog) SetupFog();
        if (adjustSun) SetupSun();
        if (applyPostProcessing) SetupPost();
    }

    void Update()
    {
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
            else if (Camera.main) player = Camera.main.transform;
        }

        scanTimer -= Time.unscaledDeltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = 0.15f;
            ScanSegments();
        }

        if (groundT && player) UpdateGround();
    }

    void OnDestroy()
    {
        if (groundT) Destroy(groundT.gameObject);
        foreach (var o in owned) if (o) Destroy(o);
        foreach (var m in roadMatMap.Values) if (m) Destroy(m);
    }

    // ================================================================== segment scan
    void ScanSegments()
    {
        gameObject.scene.GetRootGameObjects(roots);
        foreach (var go in roots)
        {
            var t = go.transform;
            if (processed.Contains(t)) continue;
            var road = t.Find(roadObjectName);
            if (!road) continue;
            var roadRenderer = road.GetComponent<MeshRenderer>();
            if (!roadRenderer) continue;

            if (!initialized) Init(roadRenderer);
            processed.Add(t);
            Decorate(t, roadRenderer);
        }
        if (++scanCount % 60 == 0) processed.RemoveWhere(x => x == null);
    }

    void Init(MeshRenderer roadRenderer)
    {
        initialized = true;
        template = roadRenderer.sharedMaterial;

        BuildTextures();
        BuildMaterials();
        BuildMeshes();

        if (grassGround)
        {
            var g = NewObj("EB_Grass", null);
            groundT = g.transform;
            var r = AddRenderer(g, quadMesh, matGrass, false);
            r.receiveShadows = true;
        }
    }

    // ================================================================== decorate
    void Decorate(Transform seg, MeshRenderer roadRenderer)
    {
        Bounds rb = roadRenderer.bounds;
        float cx = rb.center.x;
        float z0 = rb.min.z - seg.position.z;
        float len = rb.size.z;

        if (!geometryMeasured) MeasureGeometry(seg, rb);

        if (improveRoad) RetextureRoad(roadRenderer);

        if (hideOldBuildings)
            foreach (var r in seg.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("BuildingBody")) r.enabled = false;

        var decor = NewObj("EB_Decor", seg);
        decor.transform.position = new Vector3(cx, 0f, seg.position.z);
        decor.transform.rotation = Quaternion.identity;
        Transform d = decor.transform;

        var rng = new Rng(Hash((uint)Mathf.RoundToInt(seg.position.z * 10f) ^ (uint)seed * 2654435761u));

        if (laneMarkings)
        {
            float y = roadTop + 0.012f;
            if (dashedLineX != null)
                foreach (float x in dashedLineX)
                    for (float z = 0f; z + dashLength <= len + 0.01f; z += dashPeriod)
                        Place(d, "Dash", quadMesh, matLine, new Vector3(x, y, z0 + z + dashLength * 0.5f),
                              Quaternion.identity, new Vector3(lineWidth, 1f, dashLength), false);
            if (edgeLines)
            {
                float ex = Mathf.Clamp(barrierInner - edgeLineInset, 2.9f, roadHalfW - 0.2f);
                for (int s = -1; s <= 1; s += 2)
                    Place(d, "Edge", quadMesh, matLine, new Vector3(s * ex, y, z0 + len * 0.5f),
                          Quaternion.identity, new Vector3(lineWidth, 1f, len + 0.02f), false);
            }
        }

        if (shoulders)
        {
            Place(d, "ShoulderL", shoulderL, matGravel, new Vector3(0f, 0f, z0), Quaternion.identity, new Vector3(1f, 1f, len), false);
            Place(d, "ShoulderR", shoulderR, matGravel, new Vector3(0f, 0f, z0), Quaternion.identity, new Vector3(1f, 1f, len), false);
        }

        float sideStart = Mathf.Max(barrierOuter, roadHalfW) + (shoulders ? shoulderWidth : 0.5f);

        if (streetLamps && lampSpacing > 1f)
        {
            float lx = Mathf.Max(barrierOuter, roadHalfW) + 0.9f;
            for (int s = -1; s <= 1; s += 2)
            {
                float off = s < 0 ? lampSpacing * 0.5f : 0f;
                for (float z = off; z < len; z += lampSpacing)
                    Place(d, "Lamp", lampMesh, lampMats, new Vector3(s * lx, groundY, z0 + z),
                          Quaternion.Euler(0f, s < 0 ? 0f : 180f, 0f), Vector3.one, true);
            }
        }

        if (trees && treeDensity > 0f)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                float step = 3.6f / treeDensity;
                for (float z = rng.Range(0f, step); z < len; z += step * rng.Range(0.7f, 1.3f))
                {
                    if (rng.Next() < 0.12f) continue;
                    float x = sideStart + rng.Range(0.8f, 11f);
                    PlaceProp(d, ref rng, s * x, z0 + z, true);
                }
                step = 4.5f / treeDensity;
                for (float z = rng.Range(0f, step); z < len; z += step * rng.Range(0.7f, 1.3f))
                {
                    float x = sideStart + rng.Range(12f, farTreeDistance);
                    PlaceProp(d, ref rng, s * x, z0 + z, false);
                }
            }
        }

        if (hills)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                if (rng.Next() > 0.55f) continue;
                float radius = rng.Range(28f, 60f);
                float x = sideStart + farTreeDistance + radius * 1.05f + rng.Range(5f, 70f);
                float h = rng.Range(0.35f, 0.6f);
                Place(d, "Hill", hillMesh, matHill,
                      new Vector3(s * x, groundY - radius * 0.12f, z0 + rng.Range(0f, len)),
                      Quaternion.Euler(0f, rng.Range(0f, 360f), 0f),
                      new Vector3(radius, radius * h, radius * rng.Range(0.8f, 1.2f)), false);
            }
        }
    }

    void PlaceProp(Transform d, ref Rng rng, float x, float z, bool near)
    {
        float roll = rng.Next();
        Mesh mesh; Material[] mats; float scale;
        if (near)
        {
            if (roll < 0.30f) { mesh = Pick(bushMeshes, ref rng); mats = Pick(bushMats, ref rng); scale = rng.Range(0.7f, 1.3f); }
            else if (roll < 0.58f) { mesh = Pick(treeMeshes, ref rng); mats = Pick(treeMats, ref rng); scale = rng.Range(0.8f, 1.3f); }
            else if (roll < 0.86f) { mesh = Pick(pineMeshes, ref rng); mats = Pick(treeMats, ref rng); scale = rng.Range(0.8f, 1.4f); }
            else { mesh = Pick(rockMeshes, ref rng); mats = Pick(rockMats, ref rng); scale = rng.Range(0.5f, 1.4f); }
        }
        else
        {
            if (roll < 0.45f) { mesh = Pick(pineMeshes, ref rng); mats = Pick(treeMats, ref rng); scale = rng.Range(1.0f, 1.9f); }
            else if (roll < 0.85f) { mesh = Pick(treeMeshes, ref rng); mats = Pick(treeMats, ref rng); scale = rng.Range(1.0f, 1.7f); }
            else { mesh = Pick(bushMeshes, ref rng); mats = Pick(bushMats, ref rng); scale = rng.Range(1.0f, 1.8f); }
        }
        Place(d, "Prop", mesh, mats, new Vector3(x, groundY - 0.05f, z),
              Quaternion.Euler(0f, rng.Range(0f, 360f), 0f), Vector3.one * scale, near);
    }

    static T Pick<T>(T[] arr, ref Rng rng) => arr[Mathf.Min((int)(rng.Next() * arr.Length), arr.Length - 1)];

    void MeasureGeometry(Transform seg, Bounds rb)
    {
        geometryMeasured = true;
        roadHalfW = rb.extents.x;
        roadTop = rb.max.y;
        roadBottom = rb.min.y;

        float inner = float.MaxValue, outer = 0f;
        foreach (var r in seg.GetComponentsInChildren<Renderer>(true))
        {
            string n = r.name;
            if (n.IndexOf("Bariyer", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                n.IndexOf("Barrier", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            var b = r.bounds;
            float c = Mathf.Abs(b.center.x - rb.center.x);
            if (c < 1f) continue;
            inner = Mathf.Min(inner, c - b.extents.x);
            outer = Mathf.Max(outer, c + b.extents.x);
        }
        if (inner < float.MaxValue) { barrierInner = inner; barrierOuter = outer; }
        else { barrierInner = roadHalfW - 0.3f; barrierOuter = roadHalfW; }

        shoulderL = BuildShoulder(-1);
        shoulderR = BuildShoulder(1);
    }

    void RetextureRoad(MeshRenderer mr)
    {
        var mats = mr.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
        {
            var m = mats[i];
            if (!m) continue;
            if (!roadMatMap.TryGetValue(m, out var nm))
            {
                nm = new Material(m) { name = m.name + "_EB" };
                SetTex(nm, asphaltTex);
                SetColor(nm, asphaltTint);
                SetTexScale(nm, roadTiling);
                if (nm.HasProperty("_Smoothness")) nm.SetFloat("_Smoothness", 0.18f);
                if (nm.HasProperty("_Metallic")) nm.SetFloat("_Metallic", 0f);
                roadMatMap[m] = nm;
            }
            mats[i] = nm;
        }
        mr.sharedMaterials = mats;
    }

    // ================================================================== ground
    void UpdateGround()
    {
        const int halfW = 50, halfL = 44;
        float t = grassTileSize;
        float W = halfW * 2 * t, L = halfL * 2 * t;
        float cz = Mathf.Round((player.position.z + L * 0.3f) / t) * t;
        groundT.position = new Vector3(0f, groundY, cz);
        groundT.localScale = new Vector3(W, 1f, L);
        SetTexScale(matGrass, new Vector2(halfW * 2, halfL * 2));
    }

    // ================================================================== atmosphere
    void SetupFog()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
    }

    void SetupSun()
    {
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional) continue;
            l.color = sunColor;
            if (l.shadows == LightShadows.None) l.shadows = LightShadows.Soft;
            break;
        }
    }

    void SetupPost()
    {
        var go = NewObj("EB_PostFX", transform);
        go.layer = 0;
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = -5f;

        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        owned.Add(prof);
        var ca = prof.Add<ColorAdjustments>(true);
        ca.contrast.Override(10f);
        ca.saturation.Override(12f);
        var bloom = prof.Add<Bloom>(true);
        bloom.intensity.Override(0.3f);
        bloom.threshold.Override(1.0f);
        var vig = prof.Add<Vignette>(true);
        vig.intensity.Override(0.2f);
        vig.smoothness.Override(0.45f);
        vol.sharedProfile = prof;
    }

    // ================================================================== object helpers
    GameObject NewObj(string name, Transform parent)
    {
        var g = new GameObject(name) { layer = 2 };
        if (parent) g.transform.SetParent(parent, false);
        return g;
    }

    MeshRenderer AddRenderer(GameObject g, Mesh mesh, Material mat, bool shadows)
    {
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = g.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return r;
    }

    void Place(Transform parent, string name, Mesh mesh, Material mat, Vector3 lp, Quaternion lr, Vector3 ls, bool shadows)
        => Place(parent, name, mesh, new[] { mat }, lp, lr, ls, shadows);

    void Place(Transform parent, string name, Mesh mesh, Material[] mats, Vector3 lp, Quaternion lr, Vector3 ls, bool shadows)
    {
        if (!mesh) return;
        var g = NewObj(name, parent);
        g.transform.localPosition = lp;
        g.transform.localRotation = lr;
        g.transform.localScale = ls;
        var r = AddRenderer(g, mesh, mats[0], shadows);
        if (mats.Length > 1) r.sharedMaterials = mats;
    }

    // ================================================================== materials
    Material NewMat(Color c, float smooth, Texture tex = null)
    {
        Material m;
        if (template) m = new Material(template);
        else
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (!sh) sh = Shader.Find("Universal Render Pipeline/Simple Lit");
            m = new Material(sh);
        }
        foreach (var p in new[] { "_BumpMap", "_MetallicGlossMap", "_SpecGlossMap", "_OcclusionMap", "_ParallaxMap", "_EmissionMap", "_DetailAlbedoMap", "_DetailNormalMap", "_DetailMask" })
            if (m.HasProperty(p)) m.SetTexture(p, null);
        foreach (var k in new[] { "_NORMALMAP", "_PARALLAXMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP", "_EMISSION", "_DETAIL_MULX2", "_DETAIL_SCALED", "_SPECGLOSSMAP" })
            m.DisableKeyword(k);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        SetTex(m, tex);
        SetTexScale(m, Vector2.one);
        SetColor(m, c);
        m.enableInstancing = true;
        m.name = "EB_Mat";
        owned.Add(m);
        return m;
    }

    static void SetTex(Material m, Texture t)
    {
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
    }

    static void SetColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    static void SetTexScale(Material m, Vector2 s)
    {
        if (!m) return;
        if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", s);
        if (m.HasProperty("_MainTex")) m.SetTextureScale("_MainTex", s);
    }

    void BuildMaterials()
    {
        matGrass = NewMat(Color.white, 0.05f, grassTex);
        matGravel = NewMat(Color.white, 0.05f, gravelTex);
        SetTexScale(matGravel, new Vector2(1f, 10f));
        matLine = NewMat(lineColor, 0.3f);
        matBark = NewMat(new Color(0.38f, 0.26f, 0.17f), 0.05f);
        matMetal = NewMat(new Color(0.30f, 0.32f, 0.35f), 0.45f);
        matLampHead = NewMat(new Color(1.0f, 0.93f, 0.72f), 0.6f);
        matHill = NewMat(new Color(0.33f, 0.47f, 0.30f), 0.0f);

        matLeaves = new[]
        {
            NewMat(new Color(0.22f, 0.45f, 0.18f), 0.1f),
            NewMat(new Color(0.30f, 0.53f, 0.20f), 0.1f),
            NewMat(new Color(0.16f, 0.36f, 0.20f), 0.1f),
            NewMat(new Color(0.40f, 0.57f, 0.22f), 0.1f),
            NewMat(new Color(0.62f, 0.60f, 0.22f), 0.1f),
        };
        matRocks = new[]
        {
            NewMat(new Color(0.52f, 0.51f, 0.49f), 0.15f),
            NewMat(new Color(0.42f, 0.41f, 0.40f), 0.15f),
        };

        treeMats = new Material[matLeaves.Length][];
        bushMats = new Material[matLeaves.Length][];
        for (int i = 0; i < matLeaves.Length; i++)
        {
            treeMats[i] = new[] { matBark, matLeaves[i] };
            bushMats[i] = new[] { matLeaves[i] };
        }
        rockMats = new[] { new[] { matRocks[0] }, new[] { matRocks[1] } };
        lampMats = new[] { matMetal, matLampHead };
    }

    // ================================================================== textures
    Texture2D grassTex, asphaltTex, gravelTex;
    const float NoiseOfs = 37.3f;

    static float TNoise(float u, float v, float scale)
    {
        float x = u * scale, y = v * scale;
        float a = Mathf.PerlinNoise(x + NoiseOfs, y + NoiseOfs);
        float b = Mathf.PerlinNoise(x - scale + NoiseOfs, y + NoiseOfs);
        float c = Mathf.PerlinNoise(x + NoiseOfs, y - scale + NoiseOfs);
        float d = Mathf.PerlinNoise(x - scale + NoiseOfs, y - scale + NoiseOfs);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    static float Fbm(float u, float v) => 0.55f * TNoise(u, v, 4) + 0.30f * TNoise(u, v, 8) + 0.15f * TNoise(u, v, 16);

    static float Rand01(int x, int y, int salt)
    {
        unchecked { return (Hash((uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791)) & 0xFFFFFF) / 16777216f; }
    }

    Texture2D MakeTex(int size, System.Func<int, int, Color> f)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, true)
        { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = f(x, y);
        t.SetPixels32(px);
        t.Apply(true, true);
        owned.Add(t);
        return t;
    }

    void BuildTextures()
    {
        const int S = 256;
        grassTex = MakeTex(S, (x, y) =>
        {
            float u = x / (float)S, v = y / (float)S;
            float n = Fbm(u, v), big = TNoise(u, v, 2);
            Color c = Color.Lerp(new Color(0.24f, 0.40f, 0.14f), new Color(0.40f, 0.58f, 0.22f), n);
            if (big > 0.60f) c = Color.Lerp(c, new Color(0.52f, 0.54f, 0.26f), Mathf.InverseLerp(0.60f, 0.8f, big) * 0.55f);
            c *= 0.9f + 0.2f * Rand01(x, y, 1);
            c.a = 1f;
            return c;
        });

        gravelTex = MakeTex(S, (x, y) =>
        {
            float u = x / (float)S, v = y / (float)S;
            float n = Fbm(u, v);
            Color c = Color.Lerp(new Color(0.40f, 0.35f, 0.28f), new Color(0.55f, 0.50f, 0.42f), n);
            c *= 0.8f + 0.4f * Rand01(x / 2, y / 2, 3);
            c.a = 1f;
            return c;
        });

        const int A = 512;
        asphaltTex = MakeTex(A, (x, y) =>
        {
            float u = x / (float)A, v = y / (float)A;
            float n = Fbm(u, v), w = Rand01(x, y, 2), w2 = Rand01(x / 2, y / 2, 5);
            float g = 0.34f + (n - 0.5f) * 0.09f + (w - 0.5f) * 0.07f + (w2 - 0.5f) * 0.05f;
            if (w > 0.982f) g += 0.14f;
            if (w < 0.012f) g -= 0.08f;
            float patch = TNoise(u, v, 3);
            if (patch > 0.63f) g -= 0.05f * Mathf.InverseLerp(0.63f, 0.8f, patch);
            return new Color(g, g, g * 1.03f, 1f);
        });
    }

    // ================================================================== meshes
    class MB
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<int>[] t;
        public MB(int subs) { t = new List<int>[subs]; for (int i = 0; i < subs; i++) t[i] = new List<int>(); }

        public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) { var tmp = b; b = c; c = tmp; }
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            t[sub].Add(i); t[sub].Add(i + 1); t[sub].Add(i + 2);
        }

        public Mesh Build(string name, List<Object> owned)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.subMeshCount = t.Length;
            for (int i = 0; i < t.Length; i++) m.SetTriangles(t[i], i);
            m.RecalculateNormals();
            m.RecalculateBounds();
            owned.Add(m);
            return m;
        }
    }

    static void AddCyl(MB mb, int sub, Matrix4x4 m, int sides, float r0, float r1, float h, float phase = 0f)
    {
        for (int i = 0; i < sides; i++)
        {
            float a0 = phase + i * Mathf.PI * 2f / sides, a1 = phase + (i + 1) * Mathf.PI * 2f / sides;
            Vector3 p0 = new Vector3(Mathf.Cos(a0) * r0, 0, Mathf.Sin(a0) * r0);
            Vector3 p1 = new Vector3(Mathf.Cos(a1) * r0, 0, Mathf.Sin(a1) * r0);
            Vector3 q0 = new Vector3(Mathf.Cos(a0) * r1, h, Mathf.Sin(a0) * r1);
            Vector3 q1 = new Vector3(Mathf.Cos(a1) * r1, h, Mathf.Sin(a1) * r1);
            Vector3 mid = (p0 + p1 + q0 + q1) * 0.25f;
            Vector3 outw = m.MultiplyVector(new Vector3(mid.x, 0, mid.z));
            mb.Tri(sub, m.MultiplyPoint3x4(p0), m.MultiplyPoint3x4(p1), m.MultiplyPoint3x4(q0), outw);
            if (r1 > 0.0001f)
            {
                mb.Tri(sub, m.MultiplyPoint3x4(p1), m.MultiplyPoint3x4(q1), m.MultiplyPoint3x4(q0), outw);
                mb.Tri(sub, m.MultiplyPoint3x4(new Vector3(0, h, 0)), m.MultiplyPoint3x4(q0), m.MultiplyPoint3x4(q1), m.MultiplyVector(Vector3.up));
            }
            else
            {
                mb.Tri(sub, m.MultiplyPoint3x4(Vector3.zero), m.MultiplyPoint3x4(p0), m.MultiplyPoint3x4(p1), m.MultiplyVector(Vector3.down));
            }
        }
    }

    static void AddBox(MB mb, int sub, Matrix4x4 m)
    {
        Vector3[] n = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (var f in n)
        {
            Vector3 u = f.y != 0 ? Vector3.right : Vector3.up;
            Vector3 w = Vector3.Cross(f, u);
            Vector3 c = f * 0.5f;
            Vector3 a = c + (u + w) * 0.5f, b = c + (u - w) * 0.5f, d = c + (-u - w) * 0.5f, e = c + (-u + w) * 0.5f;
            Vector3 o = m.MultiplyVector(f);
            mb.Tri(sub, m.MultiplyPoint3x4(a), m.MultiplyPoint3x4(b), m.MultiplyPoint3x4(d), o);
            mb.Tri(sub, m.MultiplyPoint3x4(a), m.MultiplyPoint3x4(d), m.MultiplyPoint3x4(e), o);
        }
    }

    static List<Vector3[]> icoCache;

    static List<Vector3[]> IcoTris()
    {
        if (icoCache != null) return icoCache;
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        Vector3[] v =
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized;
        int[] f =
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };
        var tris = new List<Vector3[]>();
        for (int i = 0; i < f.Length; i += 3)
        {
            Vector3 a = v[f[i]], b = v[f[i + 1]], c = v[f[i + 2]];
            Vector3 ab = (a + b).normalized, bc = (b + c).normalized, ca = (c + a).normalized;
            tris.Add(new[] { a, ab, ca }); tris.Add(new[] { b, bc, ab });
            tris.Add(new[] { c, ca, bc }); tris.Add(new[] { ab, bc, ca });
        }
        icoCache = tris;
        return tris;
    }

    static Vector3 Jit(Vector3 p, float amp, uint salt)
    {
        unchecked
        {
            uint h = Hash((uint)Mathf.RoundToInt(p.x * 1000f) * 73856093u ^ (uint)Mathf.RoundToInt(p.y * 1000f) * 19349663u ^ (uint)Mathf.RoundToInt(p.z * 1000f) * 83492791u ^ salt);
            float r = (h & 0xFFFF) / 65535f;
            return p * (1f + (r - 0.5f) * 2f * amp);
        }
    }

    static void AddIco(MB mb, int sub, Matrix4x4 m, float jitter, uint salt)
    {
        Vector3 center = m.MultiplyPoint3x4(Vector3.zero);
        foreach (var tri in IcoTris())
        {
            Vector3 a = m.MultiplyPoint3x4(Jit(tri[0], jitter, salt));
            Vector3 b = m.MultiplyPoint3x4(Jit(tri[1], jitter, salt));
            Vector3 c = m.MultiplyPoint3x4(Jit(tri[2], jitter, salt));
            mb.Tri(sub, a, b, c, (a + b + c) / 3f - center);
        }
    }

    static Matrix4x4 TRS(Vector3 p, Vector3 euler, Vector3 s) => Matrix4x4.TRS(p, Quaternion.Euler(euler), s);

    void BuildMeshes()
    {
        quadMesh = new Mesh { name = "EB_Quad" };
        quadMesh.vertices = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, .5f), new Vector3(.5f, 0, -.5f) };
        quadMesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
        quadMesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        quadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        quadMesh.RecalculateBounds();
        owned.Add(quadMesh);

        var rng = new Rng(Hash((uint)seed + 77u));

        pineMeshes = new Mesh[3];
        for (int k = 0; k < pineMeshes.Length; k++)
        {
            var mb = new MB(2);
            AddCyl(mb, 0, Matrix4x4.identity, 6, 0.2f, 0.14f, 1.4f);
            float y = 0.9f, r = rng.Range(1.4f, 1.8f), h = rng.Range(1.9f, 2.3f);
            int layers = 3 + (k % 2);
            for (int i = 0; i < layers; i++)
            {
                AddCyl(mb, 1, TRS(new Vector3(0, y, 0), new Vector3(0, rng.Range(0f, 60f), 0), Vector3.one), 7, r, 0f, h);
                y += h * 0.5f; r *= 0.74f; h *= 0.85f;
            }
            pineMeshes[k] = mb.Build("EB_Pine", owned);
        }

        treeMeshes = new Mesh[3];
        for (int k = 0; k < treeMeshes.Length; k++)
        {
            var mb = new MB(2);
            float trunkH = rng.Range(1.8f, 2.4f);
            AddCyl(mb, 0, Matrix4x4.identity, 6, 0.22f, 0.15f, trunkH + 0.6f);
            float cr = rng.Range(1.4f, 1.8f);
            AddIco(mb, 1, TRS(new Vector3(0, trunkH + cr * 0.7f, 0), Vector3.zero, new Vector3(cr, cr * 0.9f, cr)), 0.12f, (uint)k * 11u);
            int extra = 1 + k % 2;
            for (int i = 0; i < extra; i++)
            {
                float a = rng.Range(0f, Mathf.PI * 2f), rr = cr * rng.Range(0.55f, 0.75f);
                Vector3 p = new Vector3(Mathf.Cos(a) * cr * 0.6f, trunkH + cr * rng.Range(0.4f, 1.1f), Mathf.Sin(a) * cr * 0.6f);
                AddIco(mb, 1, TRS(p, Vector3.zero, Vector3.one * rr), 0.15f, (uint)(k * 11 + i + 3));
            }
            treeMeshes[k] = mb.Build("EB_Tree", owned);
        }

        bushMeshes = new Mesh[3];
        for (int k = 0; k < bushMeshes.Length; k++)
        {
            var mb = new MB(1);
            int blobs = 2 + k;
            for (int i = 0; i < blobs; i++)
            {
                float r = rng.Range(0.55f, 0.9f);
                Vector3 p = new Vector3(rng.Range(-0.7f, 0.7f), r * 0.45f, rng.Range(-0.7f, 0.7f));
                AddIco(mb, 0, TRS(p, Vector3.zero, new Vector3(r, r * 0.75f, r)), 0.15f, (uint)(k * 7 + i + 101));
            }
            bushMeshes[k] = mb.Build("EB_Bush", owned);
        }

        rockMeshes = new Mesh[3];
        for (int k = 0; k < rockMeshes.Length; k++)
        {
            var mb = new MB(1);
            AddIco(mb, 0, TRS(new Vector3(0, 0.15f, 0), new Vector3(rng.Range(0, 20f), rng.Range(0, 360f), 0),
                              new Vector3(rng.Range(0.8f, 1.2f), rng.Range(0.45f, 0.7f), rng.Range(0.7f, 1.0f))), 0.28f, (uint)(k + 201));
            rockMeshes[k] = mb.Build("EB_Rock", owned);
        }

        {
            var mb = new MB(2);
            const float H = 6.2f;
            AddCyl(mb, 0, Matrix4x4.identity, 8, 0.16f, 0.16f, 0.5f);
            AddCyl(mb, 0, Matrix4x4.identity, 8, 0.09f, 0.07f, H);
            AddBox(mb, 0, TRS(new Vector3(0.75f, H - 0.05f, 0), Vector3.zero, new Vector3(1.6f, 0.08f, 0.08f)));
            AddBox(mb, 0, TRS(new Vector3(1.45f, H - 0.12f, 0), Vector3.zero, new Vector3(0.6f, 0.14f, 0.28f)));
            AddBox(mb, 1, TRS(new Vector3(1.45f, H - 0.21f, 0), Vector3.zero, new Vector3(0.5f, 0.04f, 0.22f)));
            lampMesh = mb.Build("EB_Lamp", owned);
        }

        {
            var mb = new MB(1);
            AddIco(mb, 0, Matrix4x4.identity, 0.18f, 999u);
            hillMesh = mb.Build("EB_Hill", owned);
        }
    }

    Mesh BuildShoulder(int side)
    {
        float x0 = side * (roadHalfW - 0.02f), x1 = side * (roadHalfW + shoulderWidth);
        float y0 = roadBottom + 0.02f, y1 = groundY + 0.015f;
        var m = new Mesh { name = "EB_Shoulder" };
        m.vertices = new[] { new Vector3(x0, y0, 0), new Vector3(x0, y0, 1), new Vector3(x1, y1, 1), new Vector3(x1, y1, 0) };
        m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
        m.triangles = side > 0 ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 };
        m.RecalculateNormals();
        m.RecalculateBounds();
        owned.Add(m);
        return m;
    }

    // ================================================================== random
    static uint Hash(uint x)
    {
        unchecked { x ^= x >> 16; x *= 0x7feb352dU; x ^= x >> 15; x *= 0x846ca68bU; x ^= x >> 16; }
        return x;
    }

    struct Rng
    {
        uint s;
        public Rng(uint seed) { s = seed == 0 ? 1u : seed; }
        public float Next() { unchecked { s ^= s << 13; s ^= s >> 17; s ^= s << 5; } return (s & 0xFFFFFF) / 16777216f; }
        public float Range(float a, float b) => a + (b - a) * Next();
    }
}