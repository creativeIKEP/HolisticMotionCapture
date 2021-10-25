using UnityEngine;

internal class Joint
{
    internal HumanBodyBones bone;
    internal HumanBodyBones parentBone;
    internal HumanBodyBones childBone;
    internal Vector3 toChildInitVector;
    internal Quaternion initRotation;
    internal Quaternion inverse;

    internal Joint(Animator avatar, HumanBodyBones bone, HumanBodyBones parent, HumanBodyBones child, Vector3 forward){
        this.bone = bone;
        parentBone = parent;
        childBone = child;
        toChildInitVector = avatar.GetBoneTransform(child).position - avatar.GetBoneTransform(bone).position;
        initRotation = avatar.GetBoneTransform(bone).rotation;
        if(bone == child) return;
        inverse = Quaternion.Inverse(Quaternion.LookRotation(avatar.GetBoneTransform(bone).position - avatar.GetBoneTransform(child).position, forward));
    }
};