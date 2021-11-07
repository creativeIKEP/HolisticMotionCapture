using System.Collections;
using System.Collections.Generic;
using UnityEngine;

partial class HolisticMotionCapture{
    Dictionary<HumanBodyBones, Joint> handJoints;

    void HandInit(){
        handJoints = new Dictionary<HumanBodyBones, Joint>();
        PerHandInit(true);
        PerHandInit(false);
    }

    void PerHandInit(bool isLeft){
        var wrist = isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
        int offset = isLeft ? 0 : 15;

        HumanBodyBones[] thumbList = new HumanBodyBones[]{wrist, HumanBodyBones.LeftThumbProximal + offset, HumanBodyBones.LeftThumbIntermediate + offset, HumanBodyBones.LeftThumbDistal + offset};
        HumanBodyBones[] indexList = new HumanBodyBones[]{wrist, HumanBodyBones.LeftIndexProximal + offset, HumanBodyBones.LeftIndexIntermediate + offset, HumanBodyBones.LeftIndexDistal + offset};
        HumanBodyBones[] middleList = new HumanBodyBones[]{wrist, HumanBodyBones.LeftMiddleProximal + offset, HumanBodyBones.LeftMiddleIntermediate + offset, HumanBodyBones.LeftMiddleDistal + offset};
        HumanBodyBones[] ringList = new HumanBodyBones[]{wrist, HumanBodyBones.LeftRingProximal + offset, HumanBodyBones.LeftRingIntermediate + offset, HumanBodyBones.LeftRingDistal + offset};
        HumanBodyBones[] littleList = new HumanBodyBones[]{wrist, HumanBodyBones.LeftLittleProximal + offset, HumanBodyBones.LeftLittleIntermediate + offset, HumanBodyBones.LeftLittleDistal + offset};

        const int handListStartIndex = 1;
        var handLists = new HumanBodyBones[][]
        {
            thumbList,
            indexList,
            middleList,
            ringList,
            littleList
        };

        var handDirection = avatar.GetBoneTransform(HumanBodyBones.LeftMiddleProximal + offset).position - avatar.GetBoneTransform(wrist).position;
        var wristToIndex = avatar.GetBoneTransform(HumanBodyBones.LeftIndexProximal + offset).position - avatar.GetBoneTransform(wrist).position;
        var handUp = Vector3.Cross(handDirection, wristToIndex);
        if(!isLeft) handUp *= -1;
        var handForward = Vector3.Cross(handUp, handDirection);
        if(!isLeft) handForward *= -1;

        for(int i=0; i<handLists.Length; i++){
            var handList = handLists[i];

            for(int j = handListStartIndex; j<handList.Length; j++){
                var boneTrans = avatar.GetBoneTransform(handList[j]);
                if(boneTrans == null) continue;
                var bone = handList[j];

                HumanBodyBones parent = wrist;
                for(int k = j - 1; k >= 0; k--){
                    boneTrans = avatar.GetBoneTransform(handList[k]);
                    if(boneTrans == null) continue;
                    parent = handList[k];
                    break;
                }

                HumanBodyBones child = bone;
                for(int k = j + 1; k < handList.Length; k++){
                    boneTrans = avatar.GetBoneTransform(handList[k]);
                    if(boneTrans == null) continue;
                    child = handList[k];
                    break;
                }
                
                var inverseRot = Quaternion.identity;
                if(bone != child){
                    inverseRot = Quaternion.Inverse(Quaternion.LookRotation(avatar.GetBoneTransform(bone).position - avatar.GetBoneTransform(child).position, handForward));
                }
                else if(avatar.GetBoneTransform(bone).childCount > 0){
                    inverseRot = Quaternion.Inverse(Quaternion.LookRotation(avatar.GetBoneTransform(bone).position - avatar.GetBoneTransform(bone).GetChild(0).position, handForward));
                }
                handJoints[bone] = new Joint(bone, parent, child, avatar.GetBoneTransform(bone).rotation, inverseRot);
            }
        }
        handJoints[wrist] = new Joint(wrist, wrist, wrist, avatar.GetBoneTransform(wrist).rotation, Quaternion.Inverse(Quaternion.LookRotation(handForward)));
    }

    void HandRender(bool isLeft, ComputeBuffer handLandmarkBuffer){
        var wrist = isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
        int offset = isLeft ? 0 : 15;

        var handLandmarks = new Vector4[handVertexCount];
        handLandmarkBuffer.GetData(handLandmarks);

        for(int i = 0; i < handVertexCount; i++){
            handLandmarks[i] = new Vector4(-handLandmarks[i].x, handLandmarks[i].y, -handLandmarks[i].z, handLandmarks[i].w);
        }

        var handDirection = handLandmarks[BoneToHolisticIndex.handTable[HumanBodyBones.LeftMiddleProximal + offset]] - handLandmarks[BoneToHolisticIndex.handTable[wrist]];
        var wristToIndex = handLandmarks[BoneToHolisticIndex.handTable[HumanBodyBones.LeftIndexProximal + offset]] - handLandmarks[BoneToHolisticIndex.handTable[wrist]];
        var handUp = Vector3.Cross(handDirection, wristToIndex);
        if(!isLeft) handUp *= -1;
        var handForward = Vector3.Cross(handUp, handDirection);
        if(!isLeft) handForward *= -1;
        
        var wristRotation = Quaternion.LookRotation(handForward, handUp) * poseJoints[HumanBodyBones.Hips].inverseRotation *  poseJoints[HumanBodyBones.Hips].initRotation;////////
        var wristTransform = avatar.GetBoneTransform(wrist);
        wristTransform.rotation = wristRotation;//Quaternion.Lerp(hipTransform.rotation, hipRotation, hipScore);

        var rotatedBones = new HumanBodyBones[]{
            HumanBodyBones.LeftThumbProximal + offset, HumanBodyBones.LeftThumbIntermediate + offset, HumanBodyBones.LeftThumbDistal + offset,
            HumanBodyBones.LeftIndexProximal + offset, HumanBodyBones.LeftIndexIntermediate + offset, HumanBodyBones.LeftIndexDistal + offset,
            HumanBodyBones.LeftMiddleProximal + offset, HumanBodyBones.LeftMiddleIntermediate + offset, HumanBodyBones.LeftMiddleDistal + offset,
            HumanBodyBones.LeftRingProximal + offset, HumanBodyBones.LeftRingIntermediate + offset, HumanBodyBones.LeftRingDistal + offset,
            HumanBodyBones.LeftLittleProximal + offset, HumanBodyBones.LeftLittleIntermediate + offset, HumanBodyBones.LeftLittleDistal + offset
        };

        foreach(var bone in rotatedBones){
            var poseJoint = handJoints[bone];
            var boneLandmarkIndex = BoneToHolisticIndex.handTable[bone];
            var childLandmarkIndex = BoneToHolisticIndex.handTable[poseJoint.childBone];
            // float score = poseLandmarks[boneLandmarkIndex].w;
            // if(score < scoreThreshold) continue;

            Vector3 toChild = handLandmarks[childLandmarkIndex] - handLandmarks[boneLandmarkIndex];
            if(boneLandmarkIndex == childLandmarkIndex){
                toChild = handLandmarks[boneLandmarkIndex + 1] - handLandmarks[boneLandmarkIndex];
            }

            var rot = Quaternion.LookRotation(-toChild, handForward) * handJoints[bone].inverseRotation * handJoints[bone].initRotation;
            var boneTrans = avatar.GetBoneTransform(bone);
            boneTrans.rotation = rot;//Quaternion.Lerp(boneTrans.rotation, rot, score);
        }
    }
}