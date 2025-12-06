using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public class RoomManager : MonoBehaviour
{
    public string roomName = "Oda İsmi";

    [Header("Lees Ayarları (Spawn)")]
    public bool isDangerous = false; 
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Guderian Ayarları (Devriye & Giriş)")]
    public bool canGuderianSpawn = false; 
    public InteractableDoor roomDoor; 
    
    // YENİ: Giriş ve Çıkış Noktaları
    [Tooltip("Kapının hemen arkası (Koridor tarafı). Guderian burada doğacak.")]
    public Transform doorOutsidePoint; 
    [Tooltip("Kapının hemen önü (Oda tarafı). Guderian içeri girince buraya yürüyecek.")]
    public Transform doorInsidePoint; 

    public List<ControllableLight> roomLights = new List<ControllableLight>();
    public List<HidingSpot> hidingSpots = new List<HidingSpot>();
    public List<Transform> guderianPatrolPoints = new List<Transform>();

    private void Reset() { ForceTrigger(); roomName = gameObject.name; }
    private void OnValidate() { ForceTrigger(); }
    private void Awake() { ForceTrigger(); }

    private void ForceTrigger()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null && !box.isTrigger) box.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && LeesEnemyAI.Instance)
{            LeesEnemyAI.Instance.EnterRoom(this);
            if (GuderianAI.Instance) GuderianAI.Instance.SetCurrentRoom(this);}
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && LeesEnemyAI.Instance)
{            LeesEnemyAI.Instance.ExitRoom(this);
            if (GuderianAI.Instance) GuderianAI.Instance.ClearRoom(this);}
    }

    public int GetActiveLightCount()
    {
        int count = 0;
        foreach (var light in roomLights) if (light != null && light.IsOn) count++;
        return count;
    }

    private void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.1f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.5f);
            Gizmos.DrawWireCube(box.center, box.size);
        }

        Gizmos.matrix = Matrix4x4.identity;

        // Lees Noktaları
        if (spawnPoints != null)
        {
            foreach (var point in spawnPoints) if (point)
            {
                Gizmos.color = isDangerous ? new Color(1f, 0.2f, 0.2f, 0.8f) : new Color(0.2f, 1f, 0.2f, 0.8f);
                Gizmos.DrawWireSphere(point.position, 0.3f);
                Gizmos.DrawRay(point.position, point.forward * 0.5f);
            }
        }
        
        // Guderian Devriye Noktaları
        if (guderianPatrolPoints != null)
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.8f); 
            for (int i = 0; i < guderianPatrolPoints.Count; i++)
            {
                Transform p = guderianPatrolPoints[i];
                if (p)
                {
                    Gizmos.DrawWireCube(p.position, Vector3.one * 0.4f);
                    if (i < guderianPatrolPoints.Count - 1 && guderianPatrolPoints[i+1])
                        Gizmos.DrawLine(p.position, guderianPatrolPoints[i+1].position);
                }
            }
        }

        // YENİ: Giriş Çıkış Noktaları
        if (doorOutsidePoint != null)
        {
            Gizmos.color = new Color(0.5f, 0f, 1f); // Mor (Dışarı)
            Gizmos.DrawSphere(doorOutsidePoint.position, 0.4f);
            Gizmos.DrawLine(doorOutsidePoint.position, doorOutsidePoint.position + Vector3.up * 2);
        }
        if (doorInsidePoint != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f); // Turuncu (İçeri)
            Gizmos.DrawSphere(doorInsidePoint.position, 0.4f);
            if(doorOutsidePoint) Gizmos.DrawLine(doorOutsidePoint.position, doorInsidePoint.position); // Giriş Rotası
        }

        // Saklanma Noktaları
        if (hidingSpots != null)
        {
            Gizmos.color = Color.cyan;
            foreach (var spot in hidingSpots) if (spot) Gizmos.DrawWireCube(spot.transform.position + Vector3.up, Vector3.one * 0.5f);
        }
    }
}