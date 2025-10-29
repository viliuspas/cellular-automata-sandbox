using UnityEngine;

public class FPSDisplay : MonoBehaviour
{
    private float fps;
    public TMPro.TextMeshProUGUI fpsText;

    void Update()
    {
        GetFPS();
    }

    void GetFPS()
    {
        fps = (int)(1f / Time.unscaledDeltaTime);
        fpsText.text = "FPS: " + fps.ToString();
    }
}
