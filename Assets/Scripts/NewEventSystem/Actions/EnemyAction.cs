using UnityEngine;

public class EnemyAction : MonoBehaviour, IAction
{
    public enum EnemyType { Global, Guderian, Lees, Adam }
    public enum EnemyCommand { Spawn, Ambush, ForceLeave, Jumpscare, StartGlobalAttack, EndGlobalAttack }

    [Tooltip("Hangi düşman veya sistem etkilenecek?")]
    public EnemyType targetEnemy = EnemyType.Guderian;

    [Tooltip("Düşmana verilecek komut")]
    public EnemyCommand command = EnemyCommand.Spawn;

    [Tooltip("Guderian'ın Spawn/Pusu atacağı oda (Sadece Guderian için gerekli)")]
    public RoomManager targetRoom;

    public void Execute()
    {
        switch (targetEnemy)
        {
            case EnemyType.Global:
                if (command == EnemyCommand.StartGlobalAttack)
                    GlobalEnemyManager.Instance?.RegisterAttackStart();
                else if (command == EnemyCommand.EndGlobalAttack)
                    GlobalEnemyManager.Instance?.RegisterAttackEnd();
                break;

            case EnemyType.Guderian:
                if (GuderianAI.Instance == null) return;
                
                if (command == EnemyCommand.Spawn && targetRoom != null)
                    GuderianAI.Instance.TrySpawnGuderian(targetRoom);
                else if (command == EnemyCommand.Ambush && targetRoom != null)
                    GuderianAI.Instance.SetupAmbush(targetRoom);
                else if (command == EnemyCommand.ForceLeave)
                    GuderianAI.Instance.ForceLeave();
                else if (command == EnemyCommand.Jumpscare)
                    GuderianAI.Instance.TriggerJumpscare();
                break;

            case EnemyType.Lees:
                if (LeesEnemyAI.Instance == null) return;
                
                if (command == EnemyCommand.Spawn)
                    LeesEnemyAI.Instance.SpawnLeesInRoom();
                else if (command == EnemyCommand.ForceLeave)
                    LeesEnemyAI.Instance.DespawnLees();
                else if (command == EnemyCommand.Jumpscare)
                    LeesEnemyAI.Instance.TriggerDeath("Event Tetikledi", false);
                break;

            case EnemyType.Adam:
                if (AdamAI.Instance == null) return;
                
                if (command == EnemyCommand.Jumpscare)
                    AdamAI.Instance.KillPlayer();
                break;
        }
        
        Debug.Log($"[EnemyAction] {targetEnemy} için {command} komutu çalıştırıldı.");
    }
}