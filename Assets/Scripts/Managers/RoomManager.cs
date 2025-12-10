using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class RoomManager : MonoBehaviour
{
    public string roomName = "Oda İsmi";

    [Tooltip("Eğer bu kutuyu işaretlersen, burası KORİDOR olur ve Adam sayacı durur.")]
    public bool isCorridor = false; // <-- BUNU İŞARETLEMEYİ UNUTMA!

    [Header("💡 Elektrik & Işık Sistemi")]
    public List<ControllableLight> roomLights = new List<ControllableLight>();

    [Header("Lees Ayarları (Spawn)")]
    public bool isDangerous = false;
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Guderian Ayarları")]
    public bool canGuderianSpawn = false;
    public InteractableDoor roomDoor;
    public Transform doorOutsidePoint;
    public Transform doorInsidePoint;
    public List<InteractableHidingSpot> hidingSpots = new List<InteractableHidingSpot>();
    public List<Transform> guderianPatrolPoints = new List<Transform>();

    private void Awake()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null && !box.isTrigger)
            box.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // --- ÖNCE ADAM ÇALIŞSIN (GARANTİ OLSUN) ---
            if (AdamAI.Instance)
                AdamAI.Instance.SetCurrentRoom(this);
            // ------------------------------------------

            if (LeesEnemyAI.Instance)
                LeesEnemyAI.Instance.EnterRoom(this);
            if (GuderianAI.Instance)
                GuderianAI.Instance.SetCurrentRoom(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // --- ÖNCE ADAM ÇALIŞSIN (GARANTİ OLSUN) ---
            if (AdamAI.Instance)
                AdamAI.Instance.ClearRoom(this);
            // ------------------------------------------

            if (LeesEnemyAI.Instance)
                LeesEnemyAI.Instance.ExitRoom(this);
            if (GuderianAI.Instance)
                GuderianAI.Instance.ClearRoom(this);
        }
    }

    public int GetActiveLightCount()
    {
        int count = 0;
        foreach (var light in roomLights)
            if (light != null && light.IsOn)
                count++;
        return count;
    }

    // Gizmos (Editör Çizimleri)
    private void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = isCorridor
                ? new Color(0, 1, 1, 0.2f)
                : new Color(1f, 0.92f, 0.016f, 0.1f); // Koridorsa Mavi, Odaysa Sarı
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}
