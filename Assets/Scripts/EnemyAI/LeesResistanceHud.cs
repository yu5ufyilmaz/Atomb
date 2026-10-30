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
        title.text = heading; title.color = Bone; detail.text = message; footer.text = controls;
        pressureLabel.text = ""; progressLabel.text = "";
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
        footer.text = workshopControls ? "FARE  /  TERS YÖNE ÇEK                         ESC  /  DURAKLAT" : "";
        pressureLabel.text = graphic.active ? "BASKI  " + Mathf.RoundToInt(model.Danger * 100).ToString("00") + "%" : "";
        progressLabel.text = graphic.active ? "DAYAN  " + Mathf.CeilToInt(model.Settings.survivalSeconds - model.Elapsed) + " sn" : "";
        if (paused && workshopControls)
        {
            title.text = "DURAKLATILDI"; detail.text = "Devam etmek için tıkla.";
            footer.text = "Mücadele ve sesler bekliyor."; return;
        }
        switch (model.State)
        {
            case LeesResistanceModel.Phase.Approaching:
                title.text = "HAREKET ETME"; detail.text = "Arkanda. Yaklaşıyor.\nBaşını çevirmesine izin verme."; break;
            case LeesResistanceModel.Phase.Struggling:
                title.text = model.PullDirection > 0 ? "SOLA DİREN" : "SAĞA DİREN";
                title.color = model.Danger > .7f ? Red : Bone;
                detail.text = model.PullDirection > 0 ? "LEES sağa çekiyor. Fareyi sola çek." : "LEES sola çekiyor. Fareyi sağa çek.";
                if (model.DirectionAge < model.Settings.directionWarningSeconds) detail.text = "TUTUŞUNU DEĞİŞTİRİYOR…  " + detail.text;
                break;
            case LeesResistanceModel.Phase.Survived:
                title.text = "KURTULDUN"; title.color = new Color(.62f, .83f, .74f);
                detail.text = "Tutuşu gevşedi. Artık arkanda değil.";
                footer.text = workshopControls ? "R  /  YENİDEN DENE" : ""; graphic.danger = 0; break;
            case LeesResistanceModel.Phase.Caught:
                title.text = "YAKALANDIN"; title.color = Red;
                detail.text = "Başını çevirdi. Mücadeleyi kaybettin.";
                footer.text = workshopControls ? "R  /  YENİDEN DENE" : ""; break;
        }
    }
}


