﻿using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UniGLTF;

namespace VRM
{
    internal static class VRMSpringBoneUtilityEditor
    {
        #region save

        public static bool SaveSpringBoneToJsonValidation()
        {
            var root = Selection.activeObject as GameObject;
            if (root == null)
            {
                return false;
            }

            var animator = root.GetComponent<Animator>();
            if (animator == null)
            {
                return false;
            }

            return true;
        }

        public static void SaveSpringBoneToJson()
        {
            var path = EditorUtility.SaveFilePanel(
                "Save spring to json",
                null,
                "VRMSpring.json",
                "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var go = Selection.activeObject as GameObject;
            var root = go.transform;
            var nodes = root.Traverse().Skip(1).ToList();
            var spring = new glTF_VRM_SecondaryAnimation();
            VRMSpringUtility.ExportSecondary(root, nodes,
                spring.colliderGroups.Add,
                spring.boneGroups.Add
            );

            var f = new UniJSON.JsonFormatter();
            VRM.VRMSerializer.Serialize_vrm_secondaryAnimation(f, spring);
            File.WriteAllBytes(path, f.GetStore().Bytes.ToArray());
        }

        #endregion

        #region load

        public static bool LoadSpringBoneFromJsonValidation()
        {
            var root = Selection.activeObject as GameObject;
            if (root == null)
            {
                return false;
            }

            var animator = root.GetComponent<Animator>();
            if (animator == null)
            {
                return false;
            }

            return true;
        }

        public static void LoadSpringBoneFromJson()
        {
            var path = EditorUtility.OpenFilePanel(
                "Load spring from json",
                null,
                "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var json = File.ReadAllText(path, Encoding.UTF8);
            var spring = JsonUtility.FromJson<glTF_VRM_SecondaryAnimation>(json);

            var go = Selection.activeObject as GameObject;
            var root = go.transform;
            var nodes = root.Traverse().Skip(1).ToList();

            VRMSpringUtility.LoadSecondary(root, nodes, spring);
        }

        #endregion

    }
}