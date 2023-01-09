using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraMove : MonoBehaviour
{
    [SerializeField] float CameraMoveSpeed;
    [SerializeField] float CameraZMoveSpeed;

    public void OnClick(BaseEventData data)
    {
        Debug.Log("click");
    }

    public void OnDrag(BaseEventData data)
    {
        Debug.Log("drag");
        var pointerData = data as PointerEventData;
        var deltaPosition = pointerData.delta * CameraMoveSpeed * Time.deltaTime;
        Camera.main.transform.localPosition += new Vector3(deltaPosition.x, -deltaPosition.y, 0);
    }

    public void OnScroll(BaseEventData data)
    {
        Debug.Log("scroll");
        var pointerData = data as PointerEventData;
        var deltaPosition = pointerData.scrollDelta.y * CameraZMoveSpeed * Time.deltaTime;
        Camera.main.transform.localPosition += new Vector3(0, 0, deltaPosition);
    }
}
