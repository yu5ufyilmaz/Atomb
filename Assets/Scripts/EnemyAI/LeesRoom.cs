using UnityEngine;
using System.Collections.Generic;

public class LeesRoom : MonoBehaviour
{
    public string roomName = "Oda İsmi";
    public bool isDangerous = false; // Lees buraya gelebilir mi?
    
    // Editör scripti burayı dolduracak
    public List<Transform> spawnPoints = new List<Transform>();

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && LeesEnemyAI.Instance != null)
        {
            LeesEnemyAI.Instance.EnterRoom(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && LeesEnemyAI.Instance != null)
        {
            LeesEnemyAI.Instance.ExitRoom(this);
        }
    }

    // Sahne ekranında noktaları görebilmek için
    private void OnDrawGizmos()
    {
        if (spawnPoints == null) return;
        foreach (var point in spawnPoints)
        {
            if (point != null)
            {
                Gizmos.color = isDangerous ? Color.red : Color.green;
                Gizmos.DrawWireSphere(point.position, 0.5f);
                Gizmos.DrawLine(point.position, point.position + point.forward * 1.5f);
            }
        }
    }
}