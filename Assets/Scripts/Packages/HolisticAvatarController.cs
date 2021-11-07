using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HolisticAvatarController
{
    #region  private variables
    Animator avatar;
    Dictionary<HumanBodyBones, Joint> poseJoints;
    Dictionary<HumanBodyBones, Joint> handJoints;
    bool isUpperBodyOnly;
    float spineRotPercentage = 0.6f;
    #endregion

    #region constant variables
    const int hipsToHeadStartIndex = 0;
    readonly HumanBodyBones[] hipsToHead = new HumanBodyBones[]{HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head};
    
    const int shoulderToHandStartIndex = 3;
    readonly HumanBodyBones[] leftShoulderToHand = new HumanBodyBones[]{
        HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, 
        HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand
    };
    readonly HumanBodyBones[] rightShoulderToHand = new HumanBodyBones[]{
        HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, 
        HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand
    };
    
    const int hipsToFootStartIndex = 1;
    readonly HumanBodyBones[] leftHipsToFoot = new HumanBodyBones[]{
        HumanBodyBones.Hips,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes
    };
    readonly HumanBodyBones[] rightHipsToFoot = new HumanBodyBones[]{
        HumanBodyBones.Hips,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes
    };
    #endregion

    #region public methods
    public HolisticAvatarController(Animator animatorAvatar){
        avatar = animatorAvatar;
        poseJoints = new Dictionary<HumanBodyBones, Joint>();

        var forward = TriangleNormal(avatar.GetBoneTransform(HumanBodyBones.Spine).position, avatar.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position, avatar.GetBoneTransform(HumanBodyBones.RightUpperLeg).position);
        var boneLists = new HumanBodyBones[][]
        {
            hipsToHead, 
            leftShoulderToHand, 
            rightShoulderToHand, 
            leftHipsToFoot, 
            rightHipsToFoot
        };
        var boneListStartIndexs = new int[]
        {
            hipsToHeadStartIndex, 
            shoulderToHandStartIndex, 
            shoulderToHandStartIndex, 
            hipsToFootStartIndex, 
            hipsToFootStartIndex
        };
        var fallbackParentBones = new HumanBodyBones[]
        {
            HumanBodyBones.Hips, 
            HumanBodyBones.Spine, 
            HumanBodyBones.Spine, 
            HumanBodyBones.Hips, 
            HumanBodyBones.Hips
        };
        var fallbackChildBones = new HumanBodyBones[]
        {
            HumanBodyBones.Head, 
            HumanBodyBones.LeftHand, 
            HumanBodyBones.RightHand, 
            avatar.GetBoneTransform(HumanBodyBones.LeftToes) != null ? HumanBodyBones.LeftToes : HumanBodyBones.LeftFoot, 
            avatar.GetBoneTransform(HumanBodyBones.RightToes) != null ? HumanBodyBones.RightToes : HumanBodyBones.RightFoot
        };

        for(int i=0; i<boneLists.Length; i++){
            var boneList = boneLists[i];
            var boneListStartIndex = boneListStartIndexs[i];
            var fallbackParentBone = fallbackParentBones[i];
            var fallbackChildBone = fallbackChildBones[i];

            for(int j = boneListStartIndex; j<boneList.Length; j++){
                var boneTrans = avatar.GetBoneTransform(boneList[j]);
                if(boneTrans == null) continue;
                var bone = boneList[j];

                HumanBodyBones parent = fallbackParentBone;
                for(int k = j - 1; k >= 0; k--){
                    boneTrans = avatar.GetBoneTransform(boneList[k]);
                    if(boneTrans == null) continue;
                    parent = boneList[k];
                    break;
                }

                HumanBodyBones child = fallbackChildBone;
                for(int k = j + 1; k < boneList.Length; k++){
                    boneTrans = avatar.GetBoneTransform(boneList[k]);
                    if(boneTrans == null) continue;
                    child = boneList[k];
                    break;
                }
                
                var inverseRot = Quaternion.identity;
                if(bone != child){
                    inverseRot = Quaternion.Inverse(Quaternion.LookRotation(avatar.GetBoneTransform(bone).position - avatar.GetBoneTransform(child).position, forward));
                }
                poseJoints[bone] = new Joint(bone, parent, child, avatar.GetBoneTransform(bone).rotation, inverseRot);
            }
        }
        
        poseJoints[HumanBodyBones.Hips].inverseRotation = Quaternion.Inverse(Quaternion.LookRotation(forward));
        poseJoints[HumanBodyBones.Head] = new Joint(HumanBodyBones.Head, HumanBodyBones.Head, HumanBodyBones.Head, avatar.GetBoneTransform(HumanBodyBones.Head).rotation, Quaternion.Inverse(Quaternion.LookRotation(forward)));


        // hand
        var leftHandDirection = avatar.GetBoneTransform(HumanBodyBones.LeftMiddleProximal).position - avatar.GetBoneTransform(HumanBodyBones.LeftHand).position;
        var wristToIndex = avatar.GetBoneTransform(HumanBodyBones.LeftIndexProximal).position - avatar.GetBoneTransform(HumanBodyBones.LeftHand).position;
        var leftHandUp = Vector3.Cross(leftHandDirection, wristToIndex);
        var leftHandForward = Vector3.Cross(leftHandUp, leftHandDirection);

        handJoints = new Dictionary<HumanBodyBones, Joint>();
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

    public void PoseRender(ComputeBuffer poseWorldBuffer, float scoreThreshold, bool isUpperBodyOnly){
        var poseLandmarks = new Vector4[34];
        poseWorldBuffer.GetData(poseLandmarks);

        // Reset pose if huamn is not visible.
        if(poseLandmarks[33].x < scoreThreshold){
            ResetPose();
            return;
        }

        // Reset pose and update pose in below if mode was changed.
        if(this.isUpperBodyOnly != isUpperBodyOnly){
            ResetPose();
            this.isUpperBodyOnly = isUpperBodyOnly;
        }

        for(int i = 0; i < 33; i++){
            poseLandmarks[i] = new Vector4(-poseLandmarks[i].x, poseLandmarks[i].y, -poseLandmarks[i].z, poseLandmarks[i].w);
        }

        // Caluculate positions of hip, neck and spine.
        var rightHipIndex = BoneToHolisticIndex.PoseTable[HumanBodyBones.RightUpperLeg];
        var leftHipIndex = BoneToHolisticIndex.PoseTable[HumanBodyBones.LeftUpperLeg];
        var rightShoulderIndex = BoneToHolisticIndex.PoseTable[HumanBodyBones.RightUpperArm];
        var leftShoulderIndex = BoneToHolisticIndex.PoseTable[HumanBodyBones.LeftUpperArm];
        Vector3 hipPosition = (poseLandmarks[rightHipIndex] + poseLandmarks[leftHipIndex]) / 2.0f;
        Vector3 neckPosition = (poseLandmarks[rightShoulderIndex] + poseLandmarks[leftShoulderIndex]) / 2.0f;
        Vector3 spinePosition = (hipPosition + neckPosition) / 2.0f;

        // Caluculate avatar forward direction and hip rotation.
        var forward = TriangleNormal(spinePosition, poseLandmarks[leftHipIndex], poseLandmarks[rightHipIndex]);
        var hipScore = (poseLandmarks[leftHipIndex].w + poseLandmarks[rightHipIndex].w) * 0.5f;
        if(hipScore > scoreThreshold && !isUpperBodyOnly){
            var hipRotation = Quaternion.LookRotation(forward, (spinePosition - hipPosition).normalized) * poseJoints[HumanBodyBones.Hips].inverseRotation *  poseJoints[HumanBodyBones.Hips].initRotation;
            var hipTransform = avatar.GetBoneTransform(HumanBodyBones.Hips);
            hipTransform.rotation = Quaternion.Lerp(hipTransform.rotation, hipRotation, hipScore);
        }
        
        var upperBodyBones = new HumanBodyBones[]{
            HumanBodyBones.RightUpperArm, 
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm
        };
        var lowerBodyBones = new HumanBodyBones[]{
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot
        };
        List<HumanBodyBones> rotatedBones = new List<HumanBodyBones>();
        rotatedBones.AddRange(upperBodyBones);
        if(!isUpperBodyOnly) rotatedBones.AddRange(lowerBodyBones);

        // Rotate arms and legs.
        foreach(var bone in rotatedBones){
            var poseJoint = poseJoints[bone];
            var boneLandmarkIndex = BoneToHolisticIndex.PoseTable[bone];
            var childLandmarkIndex = BoneToHolisticIndex.PoseTable[poseJoint.childBone];
            float score = poseLandmarks[boneLandmarkIndex].w;
            if(score < scoreThreshold) continue;

            Vector3 toChild = poseLandmarks[childLandmarkIndex] - poseLandmarks[boneLandmarkIndex];
            var rot = Quaternion.LookRotation(-toChild, forward) * poseJoints[bone].inverseRotation * poseJoints[bone].initRotation;
            var boneTrans = avatar.GetBoneTransform(bone);
            boneTrans.rotation = Quaternion.Lerp(boneTrans.rotation, rot, score);
        }

        // Rotate head with pose landmark.
        var leftEyeLandmark = poseLandmarks[2];
        var rightEyeLandmark = poseLandmarks[5];
        var leftMouthLandmark = poseLandmarks[9];
        var rightMouthLandmark = poseLandmarks[10];
        var eyeMid = (leftEyeLandmark + rightEyeLandmark) * 0.5f;
        var mouthMid = (leftMouthLandmark + rightMouthLandmark) * 0.5f;
        var headScore = (eyeMid.w + mouthMid.w) * 0.5f;
        var headForward = Vector3.Cross(eyeMid - mouthMid, leftMouthLandmark - rightMouthLandmark);
        if(headScore > scoreThreshold){
            var headRotation = Quaternion.LookRotation(headForward, (eyeMid - mouthMid).normalized) * poseJoints[HumanBodyBones.Head].inverseRotation *  poseJoints[HumanBodyBones.Head].initRotation;
            if(isUpperBodyOnly){
                var hipRotation = Quaternion.Lerp(poseJoints[HumanBodyBones.Spine].initRotation, headRotation, spineRotPercentage);
                var hipTransform = avatar.GetBoneTransform(HumanBodyBones.Spine);
                hipTransform.rotation = hipRotation;
            }
            var headTransform = avatar.GetBoneTransform(HumanBodyBones.Head);
            headTransform.rotation = Quaternion.Lerp(headTransform.rotation, headRotation, headScore);
        }
    }

    public void HandRender(bool isLeft, ComputeBuffer handLandmarkBuffer){
        var handLandmarks = new Vector4[21];
        handLandmarkBuffer.GetData(handLandmarks);

        for(int i = 0; i < 21; i++){
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
        // Rotate arms and legs.
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
    #endregion

    void ResetPose(){
        var bones = new HumanBodyBones[]{
            HumanBodyBones.Hips,
            HumanBodyBones.RightUpperArm, 
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot
        };
        foreach(var bone in bones){
            var boneTrans = avatar.GetBoneTransform(bone);
            boneTrans.rotation = poseJoints[bone].initRotation;
        }
    }

    Vector3 TriangleNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 d1 = a - b;
        Vector3 d2 = a - c;
        Vector3 dd = Vector3.Cross(d1, d2);
        dd.Normalize();
        return dd;
    }
}
