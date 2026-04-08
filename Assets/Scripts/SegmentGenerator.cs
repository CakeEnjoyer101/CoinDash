using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SegmentGenerator : MonoBehaviour
{
    public GameObject[] segment;
    [SerializeField] int zPos = 0;
    [SerializeField] bool creatingSegment = false;
    [SerializeField] int segmentNum;
    [SerializeField] float segmentSpawnDelay = 3f;
    [SerializeField] float cleanupDistanceBehindPlayer = 120f;

    readonly Queue<GameObject> spawnedSegments = new();
    PlayerMovement trackedPlayer;

    public void SetSpawnDelay(float delay)
    {
        segmentSpawnDelay = Mathf.Max(0.8f, delay);
    }

    void Update()
    {
        if (trackedPlayer == null)
            trackedPlayer = FindObjectOfType<PlayerMovement>(true);

        CleanupOldSegments();

        if(creatingSegment == false)
        {
            creatingSegment = true;
            StartCoroutine(SegmentGen());
        }
    }

    
    IEnumerator SegmentGen()
    {
        segmentNum = Random.Range(0, 3);
        var spawnedSegment = Instantiate(segment[segmentNum], new Vector3(0, 0, zPos), Quaternion.identity);
        if (spawnedSegment != null)
            spawnedSegments.Enqueue(spawnedSegment);
        zPos += 40;
        yield return new WaitForSeconds(segmentSpawnDelay);
        creatingSegment = false;
    }

    void CleanupOldSegments()
    {
        if (trackedPlayer == null)
            return;

        float playerZ = trackedPlayer.transform.position.z;
        while (spawnedSegments.Count > 0)
        {
            var oldest = spawnedSegments.Peek();
            if (oldest == null)
            {
                spawnedSegments.Dequeue();
                continue;
            }

            if (playerZ - oldest.transform.position.z < cleanupDistanceBehindPlayer)
                break;

            Destroy(oldest);
            spawnedSegments.Dequeue();
        }
    }
}
