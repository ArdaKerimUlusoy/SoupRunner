using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MapGenerator : MonoBehaviour
{
    [Header("Road Settings")]
    public GameObject roadSegmentPrefab;
    public Transform player;
    public float segmentLength = 30f;
    public int initialSegments = 6;
    public float deleteDistance = 45f;

    [Header("Obstacles")]
    public GameObject[] obstaclePrefabs;
    [Range(0f, 1f)] public float baseObstacleChance = 0.60f;
    [Range(0f, 1f)] public float maxObstacleChance = 0.95f;

    [Header("Pickups & Power-ups")]
    [Range(0f, 1f)] public float pickupSpawnChance = 0.48f;

    [Header("Lanes")]
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
            float dynamicChance = Mathf.Min(baseObstacleChance + (currentDistance * 0.0015f), maxObstacleChance);

            int checkPoints = (currentDistance > 60f) ? 2 : 1;
            List<int> blockedLanes = new List<int>();

            for (int cp = 0; cp < checkPoints; cp++)
            {
                if (obstaclePrefabs == null || obstaclePrefabs.Length == 0 || Random.value > dynamicChance) continue;

                float offsetZ = (checkPoints == 1) 
                    ? Random.Range(-segmentLength * 0.2f, segmentLength * 0.2f)
                    : (cp == 0 ? -segmentLength * 0.25f : segmentLength * 0.25f);

                float hazardZ = nextSpawnZ + offsetZ;

                List<int> availableLanes = new List<int> { 0, 1, 2 };

                int obstaclesToSpawn = 1;
                if (currentDistance > 140f && Random.value < 0.40f)
                {
                    obstaclesToSpawn = 2;
                }

                for (int i = 0; i < obstaclesToSpawn; i++)
                {
                    if (availableLanes.Count <= 1) break;

                    int laneIdx = Random.Range(0, availableLanes.Count);
                    int lane = availableLanes[laneIdx];
                    availableLanes.RemoveAt(laneIdx);
                    blockedLanes.Add(lane);

                    GameObject obsPrefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];
                    float laneX = lanePositionsX[lane];
                    Vector3 obsPos = new Vector3(laneX, 0f, hazardZ);

                    Instantiate(obsPrefab, obsPos, Quaternion.identity, newSegment.transform);
                }
            }

            if (Random.value < pickupSpawnChance)
            {
                List<int> freeLanes = new List<int>();
                for (int l = 0; l < lanePositionsX.Length; l++)
                {
                    if (!blockedLanes.Contains(l)) freeLanes.Add(l);
                }
                if (freeLanes.Count == 0) freeLanes.Add(Random.Range(0, lanePositionsX.Length));

                int chosenLane = freeLanes[Random.Range(0, freeLanes.Count)];
                float pickupZ = nextSpawnZ + Random.Range(-segmentLength * 0.2f, segmentLength * 0.2f);
                Vector3 pickupPos = new Vector3(lanePositionsX[chosenLane], 0.6f, pickupZ);

                GameObject pickupObj = new GameObject("Pickup", typeof(SoupPickup));
                pickupObj.transform.position = pickupPos;
                pickupObj.transform.SetParent(newSegment.transform, true);

                SoupPickup pickupComp = pickupObj.GetComponent<SoupPickup>();
                float roll = Random.value;
                if (roll < 0.50f)
                {
                    pickupComp.pickupType = PickupType.SoupRefill;
                }
                else if (roll < 0.80f)
                {
                    pickupComp.pickupType = PickupType.SpicyChili;
                }
                else
                {
                    pickupComp.pickupType = PickupType.GoldenLid;
                }
            }
        }

        nextSpawnZ += segmentLength;
    }
}
