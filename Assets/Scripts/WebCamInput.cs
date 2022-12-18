using UnityEngine;

public class WebCamInput
{
    // Provide input image Texture.
    public Texture inputImageTexture{
        get{
            return inputRT;
        }
    }

    WebCamTexture webCamTexture;
    RenderTexture inputRT;


    public WebCamInput(string webCamName){
        webCamTexture = new WebCamTexture(webCamName);
    }

    public WebCamInput(string webCamName, int w, int h){
        webCamTexture = new WebCamTexture(webCamName, w, h);
    }

    public Vector2 CaptureStart(){
        webCamTexture.Play();
        inputRT = new RenderTexture(webCamTexture.width, webCamTexture.height, 0);
        return new Vector2(webCamTexture.width, webCamTexture.height);
    }

    public void CaptureStop(){
        if (webCamTexture != null) webCamTexture.Stop();
        if (inputRT != null) inputRT.Release();
    }

    public void UpdateTexture()
    {
        if(!webCamTexture.didUpdateThisFrame) return;

        var aspect1 = (float)webCamTexture.width / webCamTexture.height;
        var aspect2 = (float)inputRT.width / inputRT.height;
        var aspectGap = aspect2 / aspect1;

        var vMirrored = webCamTexture.videoVerticallyMirrored;
        var scale = new Vector2(aspectGap, vMirrored ? -1 : 1);
        var offset = new Vector2((1 - aspectGap) / 2, vMirrored ? 1 : 0);

        Graphics.Blit(webCamTexture, inputRT, scale, offset);
    }
}
