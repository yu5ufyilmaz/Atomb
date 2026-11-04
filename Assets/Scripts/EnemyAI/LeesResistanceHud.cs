using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LeesResistanceHud : MonoBehaviour
{
    public TMP_Text title, detail, footer, pressureLabel, progressLabel;
    public LeesResistanceGraphic graphic;
    static readonly Color Bone = new Color(.86f, .85f, .78f);
    static readonly Color Red = new Color(.94f, .27f, .22f);

    public void ShowPrompt(string heading, string message, string controls)
    {
        gameObject.SetActive(true);
        GameLocalization.Set(title, heading); title.color = Bone; GameLocalization.Set(detail, message); GameLocalization.Set(footer, controls);
        GameLocalization.Set(pressureLabel, ""); GameLocalization.Set(progressLabel, "");
        graphic.active = false; graphic.danger = graphic.progress = 0; graphic.Refresh();
    }
    public void Present(LeesResistanceModel model, bool paused, bool workshopControls = true)
    {
        gameObject.SetActive(true);
        graphic.active = model.State == LeesResistanceModel.Phase.Struggling;
        graphic.danger = model.Danger; graphic.progress = model.Progress;
        graphic.angle = model.HeadAngle / Mathf.Max(10, model.Settings.failureAngle);
        graphic.direction = model.PullDirection;
        graphic.pull = model.PullStrength / Mathf.Max(1, model.Settings.pullDegreesPerSecond);
        graphic.clock = model.Elapsed; graphic.Refresh();
        title.color = Bone;
        GameLocalization.Set(footer, workshopControls ? "FARE  /  TERS YÖNE ÇEK                         ESC  /  DURAKLAT" : "");
        if (graphic.active)
        {
            GameLocalization.SetFormat(pressureLabel, "BASKI  {0:00}%", Mathf.RoundToInt(model.Danger * 100));
            GameLocalization.SetFormat(progressLabel, "DAYAN  {0} sn", Mathf.CeilToInt(model.Settings.survivalSeconds - model.Elapsed));
        }
        else { GameLocalization.Set(pressureLabel, ""); GameLocalization.Set(progressLabel, ""); }
        if (paused && workshopControls)
        {
            GameLocalization.Set(title, "DURAKLATILDI"); GameLocalization.Set(detail, "Devam etmek için tıkla.");
            GameLocalization.Set(footer, "Mücadele ve sesler bekliyor."); return;
        }
        switch (model.State)
        {
            case LeesResistanceModel.Phase.Approaching:
                GameLocalization.Set(title, "HAREKET ETME"); GameLocalization.Set(detail, "Arkanda. Yaklaşıyor.\nBaşını çevirmesine izin verme."); break;
            case LeesResistanceModel.Phase.Struggling:
                GameLocalization.Set(title, model.PullDirection > 0 ? "SOLA DİREN" : "SAĞA DİREN");
                title.color = model.Danger > .7f ? Red : Bone;
                string pull = model.PullDirection > 0 ? "LEES sağa çekiyor. Fareyi sola çek." : "LEES sola çekiyor. Fareyi sağa çek.";
                if (model.DirectionAge < model.Settings.directionWarningSeconds) GameLocalization.SetFormat(detail, "TUTUŞUNU DEĞİŞTİRİYOR…  {0}", pull);
                else GameLocalization.Set(detail, pull);
                break;
            case LeesResistanceModel.Phase.Survived:
                GameLocalization.Set(title, "KURTULDUN"); title.color = new Color(.62f, .83f, .74f);
                GameLocalization.Set(detail, "Tutuşu gevşedi. Artık arkanda değil.");
                GameLocalization.Set(footer, workshopControls ? "R  /  YENİDEN DENE" : ""); graphic.danger = 0; break;
            case LeesResistanceModel.Phase.Caught:
                GameLocalization.Set(title, "YAKALANDIN"); title.color = Red;
                GameLocalization.Set(detail, "Başını çevirdi. Mücadeleyi kaybettin.");
                GameLocalization.Set(footer, workshopControls ? "R  /  YENİDEN DENE" : ""); break;
        }
    }
}


