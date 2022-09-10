using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

public class Visuallizer : MonoBehaviour
{
    [SerializeField] Camera predictResultCamera;
    [SerializeField] WebCamInput webCamInput;
    [SerializeField] RawImage image;
    [SerializeField] bool isSeparateEyeBlink = false;
    [SerializeField] bool isUpperBodyOnly = false;
    [SerializeField] Shader poseShader;
    [SerializeField, Range(0, 1)] float humanPoseThreshold = 0.5f;
    [SerializeField] Shader faceShader;
    [SerializeField] Mesh faceLineTemplateMesh;
    [SerializeField, Range(0, 1)] float faceScoreThreshold = 0.5f;
    [SerializeField] Shader handShader;
    [SerializeField, Range(0, 1)] float handScoreThreshold = 0.5f;
    // Select inference type with pull down on the Unity Editor.
    [SerializeField] HolisticMocapType holisticMocapType = HolisticMocapType.full;
    [SerializeField] Animator avatarAnimator;

    HolisticMotionCapture motionCapture;
    Material poseMaterial;
    Material faceMeshMaterial;
    MaterialPropertyBlock faceMeshMaterialPropertyBlock;
    Material leftHandMaterial;
    Material rightHandMaterial;
    CommandBuffer commandBuffer;

    // Lines count of body's topology.
    const int BODY_LINE_NUM = 35;
    // Pairs of vertex indices of the lines that make up body's topology.
    // Defined by the figure in https://google.github.io/mediapipe/solutions/pose.
    readonly List<Vector4> linePair = new List<Vector4>{
        new Vector4(0, 1), new Vector4(1, 2), new Vector4(2, 3), new Vector4(3, 7), new Vector4(0, 4), 
        new Vector4(4, 5), new Vector4(5, 6), new Vector4(6, 8), new Vector4(9, 10), new Vector4(11, 12), 
        new Vector4(11, 13), new Vector4(13, 15), new Vector4(15, 17), new Vector4(17, 19), new Vector4(19, 15), 
        new Vector4(15, 21), new Vector4(12, 14), new Vector4(14, 16), new Vector4(16, 18), new Vector4(18, 20), 
        new Vector4(20, 16), new Vector4(16, 22), new Vector4(11, 23), new Vector4(12, 24), new Vector4(23, 24), 
        new Vector4(23, 25), new Vector4(25, 27), new Vector4(27, 29), new Vector4(29, 31), new Vector4(31, 27), 
        new Vector4(24, 26), new Vector4(26, 28), new Vector4(28, 30), new Vector4(30, 32), new Vector4(32, 28)
    };

    void Start()
    {
        motionCapture = new HolisticMotionCapture(avatarAnimator);

        poseMaterial = new Material(poseShader);
        faceMeshMaterial = new Material(faceShader);
        faceMeshMaterialPropertyBlock = new MaterialPropertyBlock();
        leftHandMaterial = new Material(handShader);
        rightHandMaterial = new Material(handShader);
        commandBuffer = new CommandBuffer();
        predictResultCamera.AddCommandBuffer(CameraEvent.AfterEverything, commandBuffer);
    }

    void LateUpdate()
    {
        image.texture = webCamInput.inputImageTexture;
        motionCapture.AvatarPoseRender(webCamInput.inputImageTexture, humanPoseThreshold, handScoreThreshold, faceScoreThreshold, isSeparateEyeBlink, isUpperBodyOnly, holisticMocapType);
        SetCommandBuffer();
    }

    void SetCommandBuffer() {
        commandBuffer.Clear();
        if(holisticMocapType != HolisticMocapType.face_only) PoseRender();
        if(holisticMocapType == HolisticMocapType.pose_only) return;

        if( holisticMocapType == HolisticMocapType.full || 
            holisticMocapType == HolisticMocapType.pose_and_face || 
            holisticMocapType == HolisticMocapType.face_only)
        {
            FaceRender();
        }

        if( holisticMocapType == HolisticMocapType.full || 
            holisticMocapType == HolisticMocapType.pose_and_hand)
        {
            HandRender(false);
            HandRender(true);
        }
    }

    void PoseRender(){
        var w = image.rectTransform.rect.width;
        var h = image.rectTransform.rect.height;

        // Set inferenced pose landmark results.
        poseMaterial.SetBuffer("_vertices", motionCapture.poseLandmarkBuffer);
        // Set pose landmark counts.
        poseMaterial.SetInt("_keypointCount", motionCapture.poseVertexCount);
        poseMaterial.SetFloat("_humanExistThreshold", humanPoseThreshold);
        poseMaterial.SetVector("_uiScale", new Vector2(w, h));
        poseMaterial.SetVectorArray("_linePair", linePair);

        // Draw 35 body topology lines.
        commandBuffer.DrawProcedural(Matrix4x4.identity, poseMaterial, 0, MeshTopology.Triangles, 6, BODY_LINE_NUM);

        // Draw 33 landmark points.
        commandBuffer.DrawProcedural(Matrix4x4.identity, poseMaterial, 1, MeshTopology.Triangles, 6, motionCapture.poseVertexCount);
    }

    void FaceRender(){
        var w = image.rectTransform.rect.width;
        var h = image.rectTransform.rect.height;
        faceMeshMaterial.SetVector("_uiScale", new Vector2(w, h));

        // FaceMesh
        // Set inferenced face landmark results.
        faceMeshMaterialPropertyBlock.SetBuffer("_vertices", motionCapture.faceVertexBuffer);
        commandBuffer.DrawMesh(faceLineTemplateMesh, Matrix4x4.zero, faceMeshMaterial, 0, 0, faceMeshMaterialPropertyBlock);
        faceMeshMaterial.SetBuffer("_vertices", motionCapture.faceVertexBuffer);

        // Left eye
        // Set inferenced eye landmark results.
        faceMeshMaterial.SetBuffer("_vertices", motionCapture.leftEyeVertexBuffer);
        faceMeshMaterial.SetVector("_eyeColor", Color.yellow);
        commandBuffer.DrawProcedural(Matrix4x4.identity, faceMeshMaterial, 1, MeshTopology.Lines, 64, 1);

        // Right eye
        // Set inferenced eye landmark results.
        faceMeshMaterial.SetBuffer("_vertices", motionCapture.rightEyeVertexBuffer);
        faceMeshMaterial.SetVector("_eyeColor", Color.cyan);
        commandBuffer.DrawProcedural(Matrix4x4.identity, faceMeshMaterial, 1, MeshTopology.Lines, 64, 1);
    }

    void HandRender(bool isRight){
        var w = image.rectTransform.rect.width;
        var h = image.rectTransform.rect.height;
        var handMaterial = isRight ? rightHandMaterial : leftHandMaterial;
        handMaterial.SetVector("_uiScale", new Vector2(w, h));
        handMaterial.SetVector("_pointColor", isRight ? Color.cyan : Color.yellow);
        handMaterial.SetFloat("_handScoreThreshold", handScoreThreshold);
        // Set inferenced hand landmark results.
        handMaterial.SetBuffer("_vertices", isRight ? motionCapture.rightHandVertexBuffer : motionCapture.leftHandVertexBuffer);

        // Draw 21 key point circles.
        commandBuffer.DrawProcedural(Matrix4x4.identity, handMaterial, 0, MeshTopology.Triangles, 96, motionCapture.handVertexCount);

        // Draw skeleton lines.
        commandBuffer.DrawProcedural(Matrix4x4.identity, handMaterial, 1, MeshTopology.Lines, 2, 4 * 5 + 1);
    }

    void OnDestroy(){
        // Must call Dispose method when no longer in use.
        motionCapture.Dispose();
        predictResultCamera.RemoveAllCommandBuffers();
    }
}
