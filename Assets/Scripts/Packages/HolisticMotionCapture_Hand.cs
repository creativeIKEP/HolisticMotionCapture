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

    void HandRender(HolisticMocapType mocapType, bool isLeft, float scoreThreshold){
        if(mocapType != HolisticMocapType.full && mocapType != HolisticMocapType.pose_and_hand){
            return;
        }

        var wrist = isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
        int offset = isLeft ? 0 : 15;
        var wristScore = isLeft ? holisticPipeline.leftHandDetectionScore : holisticPipeline.rightHandDetectionScore;
        if(wristScore < scoreThreshold){
            ResetHand();
            return;
        }

        var handDirection = RotateHandLandmark(BoneToHolisticIndex.handTable[HumanBodyBones.LeftMiddleProximal + offset], isLeft) - RotateHandLandmark(BoneToHolisticIndex.handTable[wrist], isLeft);
        var wristToIndex = RotateHandLandmark(BoneToHolisticIndex.handTable[HumanBodyBones.LeftIndexProximal + offset], isLeft) - RotateHandLandmark(BoneToHolisticIndex.handTable[wrist], isLeft);
        var handUp = Vector3.Cross(handDirection, wristToIndex);
        if(!isLeft) handUp *= -1;
        var handForward = Vector3.Cross(handUp, handDirection);
        if(!isLeft) handForward *= -1;

        var wristRotation = Quaternion.LookRotation(handForward, handUp) * handJoints[wrist].inverseRotation * handJoints[wrist].initRotation;
        var wristTransform = avatar.GetBoneTransform(wrist);
        wristTransform.rotation = avatar.GetBoneTransform(HumanBodyBones.Hips).rotation * Quaternion.Lerp(wristTransform.rotation, wristRotation, wristScore);
        
        var rotatedBones = new HumanBodyBones[]{
            HumanBodyBones.LeftThumbProximal + offset, HumanBodyBones.LeftThumbIntermediate + offset, HumanBodyBones.LeftThumbDistal + offset,
            HumanBodyBones.LeftIndexProximal + offset, HumanBodyBones.LeftIndexIntermediate + offset, HumanBodyBones.LeftIndexDistal + offset,
            HumanBodyBones.LeftMiddleProximal + offset, HumanBodyBones.LeftMiddleIntermediate + offset, HumanBodyBones.LeftMiddleDistal + offset,
            HumanBodyBones.LeftRingProximal + offset, HumanBodyBones.LeftRingIntermediate + offset, HumanBodyBones.LeftRingDistal + offset,
            HumanBodyBones.LeftLittleProximal + offset, HumanBodyBones.LeftLittleIntermediate + offset, HumanBodyBones.LeftLittleDistal + offset
        };

        foreach(var bone in rotatedBones){
            var handJoint = handJoints[bone];
            var boneLandmarkIndex = BoneToHolisticIndex.handTable[bone];
            var childLandmarkIndex = BoneToHolisticIndex.handTable[handJoint.childBone];

            Vector3 toChild = RotateHandLandmark(childLandmarkIndex, isLeft) - RotateHandLandmark(boneLandmarkIndex, isLeft);
            if(boneLandmarkIndex == childLandmarkIndex){
                toChild = RotateHandLandmark(boneLandmarkIndex + 1, isLeft) - RotateHandLandmark(boneLandmarkIndex, isLeft);
            }

            var rot = Quaternion.LookRotation(-toChild, handForward) * handJoints[bone].inverseRotation * handJoints[bone].initRotation;
            var boneTrans = avatar.GetBoneTransform(bone);
            boneTrans.rotation = avatar.GetBoneTransform(HumanBodyBones.Hips).rotation * Quaternion.Lerp(boneTrans.rotation, rot, wristScore);
        }
    }

    void ResetHand(){
        foreach(var handJoint in handJoints){
            var boneTrans = avatar.GetBoneTransform(handJoint.Key);
            boneTrans.localRotation = handJoints[handJoint.Key].initRotation;
        }
        avatar.GetBoneTransform(HumanBodyBones.LeftHand).localRotation = Quaternion.Euler(0, 0, 0);
        avatar.GetBoneTransform(HumanBodyBones.RightHand).localRotation = Quaternion.Euler(0, 0, 0);
    }

    Vector4 RotateHandLandmark(int index, bool isLeft){
        var landmark = isLeft ? holisticPipeline.GetLeftHandLandmark(index) : holisticPipeline.GetRightHandLandmark(index);
        return new Vector4(-landmark.x, landmark.y, -landmark.z, landmark.w);
    }
}