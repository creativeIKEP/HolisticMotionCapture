using System.Collections.Generic;
using UnityEngine;

public static class BoneRotationClamp
{
    private static Dictionary<HumanBodyBones, BoneRotationMinMax> BoneRotationMinMaxTable = new Dictionary<HumanBodyBones, BoneRotationMinMax>()
        {
            {HumanBodyBones.Hips, new BoneRotationMinMax(new Vector3 (-20, -180, -20), new Vector3(20, 180, 20))},
            {HumanBodyBones.Spine, new BoneRotationMinMax(new Vector3 (-40, -40, -40), new Vector3(40, 40, 40))},
            {HumanBodyBones.Head, new BoneRotationMinMax(new Vector3 (-40, -40, -40), new Vector3(40, 40, 40))},
            {HumanBodyBones.LeftUpperArm, new BoneRotationMinMax(new Vector3 (-90, -100, -60), new Vector3(90, 100, 100))},
            {HumanBodyBones.LeftLowerArm, new BoneRotationMinMax(new Vector3 (-90, -80, -180), new Vector3(90, 80, 180))},
            {HumanBodyBones.LeftUpperLeg, new BoneRotationMinMax(new Vector3 (-20, -20, -20), new Vector3(20, 20, 20))},
            {HumanBodyBones.LeftLowerLeg, new BoneRotationMinMax(new Vector3 (-20, 0, -180), new Vector3(20, 0, 180))},

            {HumanBodyBones.RightUpperArm, new BoneRotationMinMax(new Vector3 (-90, -100, -60), new Vector3(90, 100, 100))},
            {HumanBodyBones.RightLowerArm, new BoneRotationMinMax(new Vector3 (-90, -80, -180), new Vector3(90, 80, 180))},
            {HumanBodyBones.RightUpperLeg, new BoneRotationMinMax(new Vector3 (-40, -40, -40), new Vector3(40, 40, 40))},
            {HumanBodyBones.RightLowerLeg, new BoneRotationMinMax(new Vector3 (-40, 0, -180), new Vector3(40, 0, 180))},
        };

    public static Quaternion Clamp(Transform targetTransform, HumanBodyBones targetBone, Quaternion rotation)
    {
        var isDefinition = BoneRotationMinMaxTable.TryGetValue(targetBone, out var targetMinMaxRotation);
        if (!isDefinition) return rotation;

        var localRotation = Quaternion.Inverse(targetTransform.parent.rotation) * rotation;
        var x = localRotation.eulerAngles.x;
        if (x > 180)
        {
            x -= 360;
        }
        else if (x < -180)
        {
            x += 360;
        }
        var y = localRotation.eulerAngles.y;
        if (y > 180)
        {
            y -= 360;
        }
        else if (y < -180)
        {
            y += 360;
        }
        var z = localRotation.eulerAngles.z;
        if (z > 180)
        {
            z -= 360;
        }
        else if (z < -180)
        {
            z += 360;
        }

        x = Mathf.Min(Mathf.Max(targetMinMaxRotation.minEuler.x, x), targetMinMaxRotation.maxEuler.x);
        y = Mathf.Min(Mathf.Max(targetMinMaxRotation.minEuler.y, y), targetMinMaxRotation.maxEuler.y);
        z = Mathf.Min(Mathf.Max(targetMinMaxRotation.minEuler.z, z), targetMinMaxRotation.maxEuler.z);
        return targetTransform.parent.rotation * Quaternion.Euler(x, y, z);
    }
}

public class BoneRotationMinMax
{
    public Vector3 minEuler;
    public Vector3 maxEuler;
    public BoneRotationMinMax(Vector3 min, Vector3 max)
    {
        minEuler = min;
        maxEuler = max;
    }
}
