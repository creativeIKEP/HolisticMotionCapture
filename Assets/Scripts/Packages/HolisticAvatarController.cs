using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HolisticAvatarController
{
    public HolisticAvatarController(){
        
    }

    public void PoseRender(Animator avatar, ComputeBuffer poseWorldBuffer){
        var hoge = avatar.GetBoneTransform(HumanBodyBones.RightUpperArm);
        var fuga = avatar.GetBoneTransform(HumanBodyBones.RightLowerArm);
        var data = new Vector4[5];
        poseWorldBuffer.GetData(data, 0, 12, 5);
        data[0].x = -data[0].x;
        data[2].x = -data[2].x;
         data[0].z = -data[0].z;
        data[2].z = -data[2].z;
        Vector3 vec = data[2] - data[0];
        var rot = Quaternion.LookRotation(vec, Vector3.up) * Quaternion.FromToRotation(Vector3.right, Vector3.forward);//  * Quaternion.FromToRotation(Vector3.forward, Vector3.up);
        hoge.localRotation = rot;



        data[4].x = -data[4].x;
        // data[2].x = -data[2].x;
         data[4].z = -data[4].z;
        // data[4].z = -data[4].z;
        vec = data[4] - data[2];
        rot = Quaternion.LookRotation(vec, Vector3.up) * Quaternion.FromToRotation(Vector3.right, Vector3.forward);//  * Quaternion.FromToRotation(Vector3.forward, Vector3.up);
        fuga.localRotation = rot;

        Debug.Log("---");
        Debug.Log(data[0]);
        Debug.Log(data[2]);
    }
}
