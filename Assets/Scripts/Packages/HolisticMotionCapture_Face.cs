using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRM;

partial class HolisticMotionCapture
{
    VRMBlendShapeProxy proxy;

    void FaceInit(){
        proxy = avatar.GetComponent<VRMBlendShapeProxy>();
    }

    void FaceRender(ComputeBuffer faceVertexBuffer, ComputeBuffer leftEyeVertexBuffer, ComputeBuffer rightEyeVertexBuffer){
        var leftEyeBlink = CalculateEyeBlink(leftEyeVertexBuffer);
        var rightEyeBlink = CalculateEyeBlink(rightEyeVertexBuffer);
        
        proxy.SetValues(new Dictionary<BlendShapeKey, float>
        {
            {BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink_L), leftEyeBlink},
            {BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink_R), rightEyeBlink}
        });
    }

    float CalculateEyeBlink(ComputeBuffer eyeVertexBuffer){
        var eyeLandmarks = new Vector4[eyeVertexCount];
        eyeVertexBuffer.GetData(eyeLandmarks);

        var eyeOuterCorner = eyeLandmarks[5];
        var eyeInnerCorner = eyeLandmarks[13];
        var eyeOuterUpperLid = eyeLandmarks[16];
        var eyeOuterLowerLid = eyeLandmarks[8];
        var eyeInnerUpperLid = eyeLandmarks[18];
        var eyeInnerLowerLid = eyeLandmarks[10];

        var eyeWidth = Vector2.Distance(eyeOuterCorner, eyeInnerCorner);
        var eyeOuterLidDistance = Vector2.Distance(eyeOuterUpperLid, eyeOuterLowerLid);
        var eyeInnerLidDistance = Vector2.Distance(eyeInnerUpperLid, eyeInnerLowerLid);

        var ear = (eyeOuterLidDistance + eyeInnerLidDistance) / (2 * eyeWidth);
        var eyeOpenRatio = (ear - 0.25f) / (0.4f - 0.25f);
        var eyeBlink = 1.0f - eyeOpenRatio;
        eyeBlink = Mathf.Clamp(eyeBlink, 0, 1);
        return eyeBlink;
    }
}
