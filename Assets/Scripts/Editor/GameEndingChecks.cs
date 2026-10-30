using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class GameEndingChecks
{
    [MenuItem("Senzora/Doğrulama/Oyun Sonu Kilitleri")]
    public static void Run()
    {
        var game = GameManager.Instance;
        Require(Application.isPlaying && game != null && game.isGameStarted && !game.IsGameplayOver,
            "Oyuna girip Play'de çalıştır; test oturumu bitirir, sonra Play'den çık.");
        game.StartCoroutine(Flow(game));
    }

    private static IEnumerator Flow(GameManager game)
    {
        Require(game.BeginEnding(GameEnding.Final) && game.Ending == GameEnding.Final, "Final başlatılamadı.");
        Require(!game.BeginEnding(GameEnding.Death) && game.Ending == GameEnding.Final, "Final sürerken ölüm başlayabildi.");
        Require(GameInput.Blocked && GameInput.Scroll == 0, "Oyun sonunda girdiler açık kaldı.");
        Require(!PlayerInteraction.Instance.CanInteractWithWorld, "Oyun sonunda dünya etkileşimi açık kaldı.");
        Require(GlobalEnemyManager.Instance.stopAllEnemies && !PressureSystemManager.Instance.isSystemActive && !BreakerBox.Instance.isSystemActive,
            "Oyun sonunda düşman, basınç veya şalter çalışmaya devam ediyor.");
        var inputs = Object.FindFirstObjectByType<StarterAssets.StarterAssetsInputs>();
        Require(!inputs.enabled && Object.FindFirstObjectByType<StarterAssets.CharacterController>().IsFrozen, "Oyuncu oyun sonunda hareket edebiliyor.");
        inputs.pause = true;
        yield return null;
        yield return null;
        Require(!inputs.pause && !game.isGamePaused && Time.timeScale == 1, "Oyun sonunda duraklatma menüsü açıldı.");
        DeathUIManager.Instance.ShowDeathScreen("Oyun sonu kontrolü");
        Require(Time.timeScale == 1, "Final sırasında ölüm ekranı açıldı.");
        Debug.Log("GAME ENDING PASS: first ending wins; input, world interaction, pause, enemies, pressure and breaker stay closed.");
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
