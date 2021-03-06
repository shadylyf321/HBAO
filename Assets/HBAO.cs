﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif
[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class HBAO : MonoBehaviour
{
    /// <summary>
    /// HBAO检测方向
    /// </summary>
    public enum DIRECTION
    {
        DIRECTION_4,
        DIRECTION_6,
        DIRECTION_8,
    }
    /// <summary>
    /// HBAO特定方向统计次数
    /// </summary>
    public enum STEP
    {
        STEPS_4,
        STEPS_6,
        STEPS_8,
    }
    [SerializeField]
    DIRECTION mDir = DIRECTION.DIRECTION_4;
    [SerializeField]
    STEP mStep = STEP.STEPS_4;
    /// <summary>
    /// AO强度
    /// </summary>
    [SerializeField]
    [Range(0, 3f)]
    float mAOStrength = 0.5f;
    /// <summary>
    /// 最大检测像素半径
    /// </summary>
    [SerializeField]
    [Range(16, 256)]
    int mMaxRadiusPixel = 32;
    /// <summary>
    /// 检测半径
    /// </summary>
    [SerializeField]
    [Range(0.1f, 2)]
    float mRadius = 0.5f;
    /// <summary>
    /// 偏移角
    /// </summary>
    [SerializeField]
    [Range(0, 0.9f)]
    float mAngleBias = 0.1f;
    /// <summary>
    /// 模糊采样次数
    /// </summary>
    [SerializeField]
    bool mEnableBlur = true;
    /// <summary>
    /// 模糊半径
    /// </summary>
    [SerializeField]
    [Range(5, 20)]
    int mBlurRadiusPixel = 10;
    /// <summary>
    /// 模糊采样次数
    /// </summary>
    [SerializeField]
    [Range(2, 10)]
    int mBlurSamples = 4;
    [SerializeField]
    /// <summary>
    // 是否开启高斯模糊
    /// </summary>
    bool mGuassBlur = false;
    [SerializeField]
    Shader mHbaoShader;
    private static class ShaderProperties
    {
        public static int MainTex;
        public static int HbaoTex;
        public static int HbaoBlurTex;
        public static int UV2View;
        public static int TexelSize;
        public static int AOStrength;
        public static int MaxRadiusPixel;
        public static int RadiusPixel;
        public static int Radius;
        public static int AngleBias;
        public static int BlurRadiusPixel;
        public static int BlurSamples;
        public static int BlurDir;
        static ShaderProperties()
        {
            MainTex = Shader.PropertyToID("_MainTex");
            HbaoTex = Shader.PropertyToID("_HbaoTex");
            HbaoBlurTex = Shader.PropertyToID("_HbaoBlurTex");
            UV2View = Shader.PropertyToID("_UV2View");
            TexelSize = Shader.PropertyToID("_TexelSize");
            AOStrength = Shader.PropertyToID("_AOStrengh");
            MaxRadiusPixel = Shader.PropertyToID("_MaxRadiusPixel");
            RadiusPixel = Shader.PropertyToID("_RadiusPixel");
            Radius = Shader.PropertyToID("_Radius");
            AngleBias = Shader.PropertyToID("_AngleBias");
            BlurRadiusPixel = Shader.PropertyToID("_BlurRadiusPixel");
            BlurSamples = Shader.PropertyToID("_BlurSamples");
            BlurDir = Shader.PropertyToID("_BlurDir");
        }
    }
    private string[] mShaderKeywords = new string[4] 
        {
            "DIRECTION_4" ,
            "STEPS_4",
            "ENABLEBLUR",
            "GUASSBLUR",
        };

    private static class Pass
    {
        public const int AO = 0;
        public const int Composite = 1;
        public const int Blur = 2;
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

    void UpdateMaterialProperties()
    {
        var tanHalfFovY = Mathf.Tan(mCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        var tanHalfFovX = tanHalfFovY * ((float)mCamera.pixelWidth / mCamera.pixelHeight);
        //计算相机空间:x = (2* u - 1) * tanHalfFovX * depth  (2u - 1将坐标映射到-1,1)
        mMaterial.SetVector(ShaderProperties.UV2View, new Vector4(2 * tanHalfFovX, 2 * tanHalfFovY, -tanHalfFovX, -tanHalfFovY));
        mMaterial.SetVector(ShaderProperties.TexelSize,
            new Vector4(1f / mCamera.pixelWidth, 1f / mCamera.pixelHeight, mCamera.pixelWidth, mCamera.pixelHeight));
        //当z=1时,半径为radius对应的屏幕像素
        mMaterial.SetFloat(ShaderProperties.RadiusPixel, mCamera.pixelHeight * mRadius / tanHalfFovY / 2);
        mMaterial.SetFloat(ShaderProperties.Radius, mRadius);
        mMaterial.SetFloat(ShaderProperties.MaxRadiusPixel, mMaxRadiusPixel);
        mMaterial.SetFloat(ShaderProperties.AngleBias, mAngleBias);
        mMaterial.SetFloat(ShaderProperties.BlurRadiusPixel, mBlurRadiusPixel);
        mMaterial.SetInt(ShaderProperties.BlurSamples, mBlurSamples);
        mMaterial.SetFloat(ShaderProperties.AOStrength, mAOStrength);
    }

    void UpdateShaderKeywords()
    {
        mShaderKeywords[0] = mDir.ToString();
        mShaderKeywords[1] = mStep.ToString();
        mShaderKeywords[2] = mEnableBlur ? "ENABLEBLUR" : "__";
        mShaderKeywords[3] = mGuassBlur ? "GUASSBLUR" : "__";

        mMaterial.shaderKeywords = mShaderKeywords;
    }

    void BlitFullscreenTriangle(CommandBuffer cmd,  RenderTargetIdentifier source, RenderTargetIdentifier dest, Material mat, int pass)
    {
        cmd.SetGlobalTexture(ShaderProperties.MainTex, source);
        cmd.SetRenderTarget(dest);
        cmd.DrawMesh(mFullscreenTriangle, Matrix4x4.identity, mat, 0, pass);
    }

    void OnPreRender()
    {
        ClearCommandBuffer(mCmdBuffer);
        UpdateMaterialProperties();
        UpdateShaderKeywords();
        BuildCommandBuffer(mCmdBuffer);
    }

    void BuildCommandBuffer(CommandBuffer cmd)
    {
        AOEffect(cmd);
        Blur(cmd);
        Composite(cmd);
        cmd.ReleaseTemporaryRT(ShaderProperties.HbaoTex);
        if (mEnableBlur)
            cmd.ReleaseTemporaryRT(ShaderProperties.HbaoBlurTex);
        mCamera.AddCommandBuffer(CameraEvent.BeforeImageEffectsOpaque, cmd);
    }

    void AOEffect(CommandBuffer cmd)
    {
        GetScreenSpaceTemporaryRT(cmd, ShaderProperties.HbaoTex);
        BlitFullscreenTriangle(cmd, BuiltinRenderTextureType.CameraTarget, ShaderProperties.HbaoTex, mMaterial, Pass.AO);
    }

    void Blur(CommandBuffer cmd)
    {
        if (mEnableBlur)
        {
            GetScreenSpaceTemporaryRT(cmd, ShaderProperties.HbaoBlurTex);
            mMaterial.SetVector(ShaderProperties.BlurDir, new Vector2(1, 0));
            BlitFullscreenTriangle(cmd, ShaderProperties.HbaoTex, ShaderProperties.HbaoBlurTex, mMaterial, Pass.Blur);
            mMaterial.SetVector(ShaderProperties.BlurDir, new Vector2(0, 1));
            BlitFullscreenTriangle(cmd, ShaderProperties.HbaoBlurTex, ShaderProperties.HbaoTex, mMaterial, Pass.Blur);
        }
    }

    void Composite(CommandBuffer cmd)
    {
        BlitFullscreenTriangle(cmd, BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.CameraTarget, mMaterial, Pass.Composite);
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
