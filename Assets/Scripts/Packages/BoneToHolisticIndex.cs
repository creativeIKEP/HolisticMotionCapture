using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class BoneToHolisticIndex{
    public static Dictionary<HumanBodyBones, int> PoseTable = new Dictionary<HumanBodyBones, int>(){
        // Right arm
        {HumanBodyBones.RightUpperArm, 12},
        {HumanBodyBones.RightLowerArm, 14},
        {HumanBodyBones.RightHand, 16},
        
        // Left arm
        {HumanBodyBones.LeftUpperArm, 11},
        {HumanBodyBones.LeftLowerArm, 13},
        {HumanBodyBones.LeftHand, 15},

        // Right leg
        {HumanBodyBones.RightUpperLeg, 24},
        {HumanBodyBones.RightLowerLeg, 26},
        {HumanBodyBones.RightFoot, 28},
        {HumanBodyBones.RightToes, 32},

        // Left leg
        {HumanBodyBones.LeftUpperLeg, 23},
        {HumanBodyBones.LeftLowerLeg, 25},
        {HumanBodyBones.LeftFoot, 27},
        {HumanBodyBones.LeftToes, 31}
    };
}
