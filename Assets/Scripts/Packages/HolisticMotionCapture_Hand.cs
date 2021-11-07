using System.Collections;
using System.Collections.Generic;
using UnityEngine;

partial class HolisticMotionCapture{
    Dictionary<HumanBodyBones, Joint> handJoints;

    void HandInit(){
        HumanBodyBones[] leftThumbList = new HumanBodyBones[]{HumanBodyBones.LeftHand, HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal};
        HumanBodyBones[] leftIndexList = new HumanBodyBones[]{HumanBodyBones.LeftHand, HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal};
        HumanBodyBones[] leftMiddleList = new HumanBodyBones[]{HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal};
        HumanBodyBones[] leftRingList = new HumanBodyBones[]{HumanBodyBones.LeftHand, HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal};
        HumanBodyBones[] leftLittleList = new HumanBodyBones[]{HumanBodyBones.LeftHand, HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal};

        const int handListStartIndex = 1;
        var handLists = new HumanBodyBones[][]
        {
            leftThumbList,
            leftIndexList,
            leftMiddleList,
            leftRingList,
            leftLittleList
        };
        var fallbackParentBones_hand = new HumanBodyBones[]
        {
            HumanBodyBones.LeftHand, 
            HumanBodyBones.LeftHand, 
            HumanBodyBones.LeftHand, 
            HumanBodyBones.LeftHand, 
            HumanBodyBones.LeftHand
        };

        handJoints = new Dictionary<HumanBodyBones, Joint>();
        var leftHandDirection = avatar.GetBoneTransform(HumanBodyBones.LeftMiddleProximal).position - avatar.GetBoneTransform(HumanBodyBones.LeftHand).position;
        var wristToIndex = avatar.GetBoneTransform(HumanBodyBones.LeftIndexProximal).position - avatar.GetBoneTransform(HumanBodyBones.LeftHand).position;
        var leftHandUp = Vector3.Cross(leftHandDirection, wristToIndex);
        var leftHandForward = Vector3.Cross(leftHandUp, leftHandDirection);

        for(int i=0; i<handLists.Length; i++){
            var handList = handLists[i];
            var fallbackParentBone_hand = fallbackParentBones_hand[i];

            for(int j = handListStartIndex; j<handList.Length; j++){
                var boneTrans = avatar.GetBoneTransform(handList[j]);
                if(boneTrans == null) continue;
                var bone = handList[j];

                HumanBodyBones parent = fallbackParentBone_hand;
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
                    inverseRot = Quaternion.Inverse(Quaternion.LookRotation(avatar.GetBoneTransform(bone).position - avatar.GetBoneTransform(child).position, leftHandForward));
                }
                else if(avatar.GetBoneTransform(bone).childCount > 0){
                    inverseRot = Quaternion.Inverse(Quaternion.LookRotation(avatar.GetBoneTransform(bone).position - avatar.GetBoneTransform(bone).GetChild(0).position, leftHandForward));
                }
                handJoints[bone] = new Joint(bone, parent, child, avatar.GetBoneTransform(bone).rotation, inverseRot);
            }
        }
        handJoints[HumanBodyBones.LeftHand] = new Joint(HumanBodyBones.LeftHand, HumanBodyBones.LeftHand, HumanBodyBones.LeftHand, avatar.GetBoneTransform(HumanBodyBones.LeftHand).rotation, Quaternion.Inverse(Quaternion.LookRotation(leftHandForward)));
    }

    void HandRender(bool isLeft, ComputeBuffer handLandmarkBuffer){
        var handLandmarks = new Vector4[handVertexCount];
        handLandmarkBuffer.GetData(handLandmarks);

        for(int i = 0; i < handVertexCount; i++){
            handLandmarks[i] = new Vector4(-handLandmarks[i].x, handLandmarks[i].y, -handLandmarks[i].z, handLandmarks[i].w);
        }

        var leftHandDirection = handLandmarks[BoneToHolisticIndex.handTable[HumanBodyBones.LeftMiddleProximal]] - handLandmarks[BoneToHolisticIndex.handTable[HumanBodyBones.LeftHand]];
        var wristToIndex = handLandmarks[BoneToHolisticIndex.handTable[HumanBodyBones.LeftIndexProximal]] - handLandmarks[BoneToHolisticIndex.handTable[HumanBodyBones.LeftHand]];
        var leftHandUp = Vector3.Cross(leftHandDirection, wristToIndex);
        var leftHandForward = Vector3.Cross(leftHandUp, leftHandDirection);
        var leftHandRotation = Quaternion.LookRotation(leftHandForward, leftHandUp) * poseJoints[HumanBodyBones.Hips].inverseRotation *  poseJoints[HumanBodyBones.Hips].initRotation;
        var leftHandTransform = avatar.GetBoneTransform(HumanBodyBones.LeftHand);
        leftHandTransform.rotation = leftHandRotation;//Quaternion.Lerp(hipTransform.rotation, hipRotation, hipScore);

        var rotatedBones = new HumanBodyBones[]{
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal
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

            var rot = Quaternion.LookRotation(-toChild, leftHandForward) * handJoints[bone].inverseRotation * handJoints[bone].initRotation;
            var boneTrans = avatar.GetBoneTransform(bone);
            boneTrans.rotation = rot;//Quaternion.Lerp(boneTrans.rotation, rot, score);
        }
    }
}