using UnityEngine;
using System.Collections.Generic;

// BU SATIR ÇOK ÖNEMLİ: Script eklendiği an BoxCollider'ı zorla ekler
[RequireComponent(typeof(BoxCollider))]
public class LeesRoom : MonoBehaviour
{
    public string roomName = "Oda İsmi";
    public bool isDangerous = false; 
    
    // Editör tarafından yönetilen spawn noktaları
    public List<Transform> spawnPoints = new List<Transform>();

    // --- OTOMATİK AYARLAR ---
    // Scripti objeye ilk attığında veya 'Reset' dediğinde çalışır
    private void Reset()
    {
        // Collider'ı al ve Trigger yap
        BoxCollider box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        
        // İsmi otomatik ayarla
        roomName = gameObject.name;
    }

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

    // --- GÖRSELLİK (GIZMOS) ---
    private void OnDrawGizmos()
    {
        // 1. Odanın Sınırlarını Çiz (Sarı Kutu)
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            // Odanın içini şeffaf sarı yap
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.2f); 
            Gizmos.matrix = transform.localToWorldMatrix; // Döndürmeyi destekle
            Gizmos.DrawCube(box.center, box.size);
            
            // Çerçevesini tam sarı yap
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(box.center, box.size);
        }

        // 2. Spawn Noktalarını Çiz
        if (spawnPoints == null) return;
        Gizmos.matrix = Matrix4x4.identity; // Matrix'i sıfırla

        foreach (var point in spawnPoints)
        {
            if (point != null)
            {
                Gizmos.color = isDangerous ? Color.red : Color.green;
                Gizmos.DrawWireSphere(point.position, 0.5f);
                Gizmos.DrawLine(point.position, point.position + point.forward * 1.5f); // Yön oku
            }
        }
    }
}