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

    void FaceRender(ComputeBuffer faceVertexBuffer, ComputeBuffer leftEyeVertexBuffer, ComputeBuffer rightEyeVertexBuffer, bool isSeparateEyeBlink){
        var leftEyeBlink = CalculateEyeBlink(leftEyeVertexBuffer);
        var rightEyeBlink = CalculateEyeBlink(rightEyeVertexBuffer);
        
        if(isSeparateEyeBlink){
            proxy.SetValues(new Dictionary<BlendShapeKey, float>
            {
                {BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink_L), leftEyeBlink},
                {BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink_R), rightEyeBlink}
            });
        }
        else{
            var blink = IntegratedBlink(leftEyeBlink, rightEyeBlink);
            proxy.SetValues(new Dictionary<BlendShapeKey, float>
            {
                {BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink), blink}
            });
        }
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
        var maxRatio = 0.5f;
        var minRatio = 0.3f;
        var eyeOpenRatio = (ear - minRatio) / (maxRatio - minRatio);
        var eyeBlink = 1.0f - eyeOpenRatio;
        eyeBlink = Mathf.Clamp(eyeBlink, 0, 1);
        return eyeBlink;
    }

    float IntegratedBlink(float leftEyeBlink, float rightEyeBlink){
        var headRot = avatar.GetBoneTransform(HumanBodyBones.Head).rotation;
        if(0 <= headRot.eulerAngles.y && headRot.eulerAngles.y <= 180){
            return leftEyeBlink;
        }
        return rightEyeBlink;
    }
}
