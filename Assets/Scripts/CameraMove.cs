using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraMove : MonoBehaviour
{
    [SerializeField] float CameraMoveSpeed;
    [SerializeField] float CameraZMoveSpeed;
    [SerializeField] CanvasGroup controlCanvas;
    [SerializeField] Camera PredictionCamera;

    bool isDragging = false;
    bool isUiShow = true;

    public void OnClick(BaseEventData data)
    {
        var pointerData = data as PointerEventData;
        if (pointerData.dragging) return;
        isUiShow = !isUiShow;
        controlCanvas.alpha = isUiShow ? 1 : 0;
        controlCanvas.interactable = isUiShow;
        controlCanvas.blocksRaycasts = isUiShow;
        PredictionCamera.enabled = isUiShow;
    }

    public void OnDrag(BaseEventData data)
    {
        var pointerData = data as PointerEventData;
        var deltaPosition = pointerData.delta * CameraMoveSpeed * Time.deltaTime;
        Camera.main.transform.localPosition += new Vector3(deltaPosition.x, -deltaPosition.y, 0);
    }

    public void OnScroll(BaseEventData data)
    {
        var pointerData = data as PointerEventData;
        var deltaPosition = pointerData.scrollDelta.y * CameraZMoveSpeed * Time.deltaTime;
        Camera.main.transform.localPosition += new Vector3(0, 0, deltaPosition);
    }
}
