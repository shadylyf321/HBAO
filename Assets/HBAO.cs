using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class HBAO : MonoBehaviour
{
    [SerializeField]
    Shader mHbaoShader;
    private static class ShaderProperties
    {
        public static int mainTex;
        public static int hbaoTex;
        static ShaderProperties()
        {
            mainTex = Shader.PropertyToID("_MainTex");
            hbaoTex = Shader.PropertyToID("_HbaoTex");
        }
    }

    private static class Pass
    {
        public const int AO = 0;
        public const int Blur = 1;
        public const int Composite = 3;
    }

    //使用一个大三角形绘制renderTarget
    //https://forum.unity.com/threads/use-a-giant-triangle-instead-of-quad-for-postprocessing.664465/
    private Mesh mFullscreenTriangle;
    private Material mMaterial;
    private RenderTextureDescriptor mRtDescriptor;
    private CommandBuffer mCmdBuffer;
    Camera _camera;
    private Camera mCamera
    {
        get
        {
            if (_camera != null)
                return _camera;
            _camera = GetComponent<Camera>();
            _camera.depthTextureMode = DepthTextureMode.DepthNormals;
            return _camera;
        }
    }


    void Initialize()
    {
        mFullscreenTriangle = new Mesh { name = "Fullscreen Triangle" };
        mFullscreenTriangle.SetVertices(new List<Vector3>
            {
                new Vector3(-1f, -1f, 0f),
                new Vector3(-1f, 3f, 0f),
                new Vector3(3f, -1f, 0f)
            });
        mFullscreenTriangle.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 0, false);
        mFullscreenTriangle.UploadMeshData(false);
        mMaterial = new Material(mHbaoShader);
        mRtDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default);
        mCmdBuffer = new CommandBuffer() { name = "HBAO"};
    }

    void UnInitialize()
    {
        DestroyImmediate(mMaterial);
        DestroyImmediate(mFullscreenTriangle);
        ClearCommandBuffer(mCmdBuffer);
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void OnDisable()
    {
        UnInitialize();
    }

    void BlitFullscreenTriangle(CommandBuffer cmd,  RenderTargetIdentifier source, RenderTargetIdentifier dest, Material mat, int pass)
    {
        cmd.SetGlobalTexture(ShaderProperties.mainTex, source);
        cmd.SetRenderTarget(ShaderProperties.mainTex);
        cmd.DrawMesh(mFullscreenTriangle, Matrix4x4.identity, mat, 0, pass);
    }

    void OnPreRender()
    {
        ClearCommandBuffer(mCmdBuffer);
        BuildCommandBuffer(mCmdBuffer);
    }

    void BuildCommandBuffer(CommandBuffer cmd)
    {
        GetScreenSpaceTemporaryRT(cmd, ShaderProperties.hbaoTex);
        AOEffect(cmd);
        mCamera.AddCommandBuffer(CameraEvent.BeforeImageEffectsOpaque, cmd);
    }

    void AOEffect(CommandBuffer cmd)
    {
        BlitFullscreenTriangle(cmd, BuiltinRenderTextureType.CameraTarget, ShaderProperties.hbaoTex, mMaterial, Pass.AO);
    }

    void GetScreenSpaceTemporaryRT(CommandBuffer cmd, int nameID)
    {
        cmd.GetTemporaryRT(nameID, mRtDescriptor, FilterMode.Bilinear);
    }

    private void ClearCommandBuffer(CommandBuffer cmd)
    {
        if (cmd != null)
        {
            if (mCamera != null)
            {
                mCamera.RemoveCommandBuffer(CameraEvent.BeforeImageEffectsOpaque, cmd);
                mCamera.RemoveCommandBuffer(CameraEvent.AfterLighting, cmd);
                mCamera.RemoveCommandBuffer(CameraEvent.BeforeReflections, cmd);
            }
            cmd.Clear();
        }
    }
}
