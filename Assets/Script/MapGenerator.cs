using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MapGenerator : MonoBehaviour
{
    [Header("Yol Parçasý Ayarlarý")]
    public GameObject roadSegmentPrefab;
    public Transform player;
    public float segmentLength = 30f;
    public int initialSegments = 6;
    public float deleteDistance = 45f;

    [Header("Engeller")]
    public GameObject[] obstaclePrefabs;
    [Range(0f, 1f)] public float baseObstacleChance = 0.50f; // Baþlangýç engeli çýkma þansý %50
    [Range(0f, 1f)] public float maxObstacleChance = 0.95f;  // Ýlerledikçe maksimum þans %95

    [Header("Þerit Pozisyonlarý")]
    public float[] lanePositionsX = new float[] { -2.4f, 0f, 2.4f };

    private float nextSpawnZ = 0f;
    private float startZ = 0f;
    private List<GameObject> activeSegments = new List<GameObject>();

    private void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        if (player != null)
        {
            startZ = player.position.z;
        }

        // Ýlk 2 yol boþ ve rahat baþlasýn
        for (int i = 0; i < initialSegments; i++)
        {
            SpawnSegment(i > 1);
        }
    }

    private void Update()
    {
        if (player == null) return;

        if (player.position.z + (initialSegments * segmentLength) > nextSpawnZ)
        {
            SpawnSegment(true);
        }

        if (activeSegments.Count > 0 && activeSegments[0] != null)
        {
            if (player.position.z - activeSegments[0].transform.position.z > deleteDistance)
            {
                GameObject oldSeg = activeSegments[0];
                activeSegments.RemoveAt(0);

#if UNITY_EDITOR
                if (Selection.activeGameObject == oldSeg || (oldSeg != null && Selection.Contains(oldSeg)))
                {
                    Selection.activeGameObject = null;
                }
#endif

                Destroy(oldSeg);
            }
        }
    }

    private void SpawnSegment(bool spawnHazards)
    {
        if (roadSegmentPrefab == null) return;

        Vector3 spawnPos = new Vector3(0f, 0f, nextSpawnZ);
        GameObject newSegment = Instantiate(roadSegmentPrefab, spawnPos, Quaternion.identity);
        activeSegments.Add(newSegment);

        if (spawnHazards && player != null)
        {
            float currentDistance = Mathf.Max(0f, player.position.z - startZ);

            // Mesafe arttýkça engel çýkma oraný artar
            float dynamicChance = Mathf.Min(baseObstacleChance + (currentDistance * 0.001f), maxObstacleChance);

            List<int> availableLanes = new List<int> { 0, 1, 2 };

            // 180 metreden sonra zaman zaman 2 þeridi birden kapatacak tuzaklar kurar
            int obstaclesToSpawn = 1;
            if (currentDistance > 180f && Random.value < 0.45f)
            {
                obstaclesToSpawn = 2;
            }

            if (obstaclePrefabs != null && obstaclePrefabs.Length > 0 && Random.value < dynamicChance)
            {
                for (int i = 0; i < obstaclesToSpawn; i++)
                {
                    if (availableLanes.Count == 0) break;

                    int laneIdx = Random.Range(0, availableLanes.Count);
                    int lane = availableLanes[laneIdx];
                    availableLanes.RemoveAt(laneIdx);

                    GameObject obsPrefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];
                    float laneX = lanePositionsX[lane];
                    float randomZOffset = Random.Range(-segmentLength * 0.25f, segmentLength * 0.25f);
                    Vector3 obsPos = new Vector3(laneX, 0f, nextSpawnZ + randomZOffset);

                    Instantiate(obsPrefab, obsPos, Quaternion.identity, newSegment.transform);
                }
            }
        }

        nextSpawnZ += segmentLength;
    }
}