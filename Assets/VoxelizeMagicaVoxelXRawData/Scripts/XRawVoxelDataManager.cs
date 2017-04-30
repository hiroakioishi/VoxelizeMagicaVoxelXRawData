using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace irishoak.VoxelizeMagicaVoxelXRawData
{
    public class XRawVoxelDataManager : MonoBehaviour
    {

        #region Structs
        struct VoxelData
        {
            public Vector3 Position;
            public Vector4 Rotation;
            public float   Scale;
            public Vector4 Color;
        };
        #endregion

        #region Properties
        /// <summary>生成するボクセルの数(x軸)</summary>
        [Header("Voxel Params")]
        public int VoxelNumWidth  = 64;
        /// <summary>生成するボクセルの数(y軸)</summary>
        public int VoxelNumHeight = 64;
        /// <summary>生成するボクセルの数(z軸)</summary>
        public int VoxelNumDepth  = 64;

        /// <summary>ボクセルの大きさ</summary>
        public float VoxelScale          =  0.25f;
        /// <summary>ボクセルの集合の幅の大きさ</summary>
        public float TotalVoxelGridScale = 16.0f;

        [Header("XRawDataTex Parms")]
        public Texture2D XRawDataTex = null;
        /// <summary>.xrawデータをテクスチャに変換したときのテクスチャの幅</summary>
        public int XRawDataTexWidth    = 1024;
        /// <summary>.xrawデータをテクスチャに変換したときのテクスチャの高さ</summary>
        public int XRawDataTexHeight   = 1024;
        /// <summary>MagicaVoxel上でのx軸のサイズ</summary>
        public int XRawDataVoxelWidth  = 96;
        /// <summary>MagicaVoxel上でのy軸のサイズ</summary>
        public int XRawDataVoxelHeight = 96;
        /// <summary>MagicaVoxel上でのz軸のサイズ</summary>
        public int XRawDataVoxelDepth  = 96;

        /// <summary>セットしたテクスチャから上のパラメータをセット</summary>
        public bool TriggerSetXRawDataParams = false;
        /// <summary>ボクセル化</summary>
        public bool TriggerVoxelize = false;

        [Header("Voxel Render Params")]
        [Range(0.0f, 1.0f)]
        public float VoxelScaleRate = 1.0f;
        public Transform Distractor = null;

        public Color AmbientColor = new Color(0.3f, 0.3f, 1.0f, 1.0f);
        [Range(0.01f, 1.0f)]
        public float Ka = 0.5f;

        //public Color DiffuseColor = new Color(0.3f, 0.3f, 1.0f, 1.0f);
        [Range(0.01f, 1.0f)]
        public float Kd = 0.8f;

        public Color SpecularColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        [Range(0.01f, 1.0f)]
        public float Ks = 1.0f;
        public float Shinness = 0.7f;

        #endregion

        #region Built-in Resources
        [Header("Built-in Resources")]
        [SerializeField]
        ComputeShader _cs = null;

        [SerializeField]
        Shader   _voxelRenderShader = null;

        Material _voxelRenderMat = null;
        #endregion

        #region Private Resources and Variables
        ComputeBuffer _targetVoxelDataBuffer = null;
        ComputeBuffer _targetVoxelDataDeadListBuffer = null;
        
        int _totalVoxelNum = 0;

        bool _isInit = false; 
        #endregion

        #region MonoBehaviour Functions
        private void Update()
        {
            if (!_isInit)
            {
                InitResources();

                _isInit = true;
            }
            else
            {
                if (TriggerSetXRawDataParams)
                {
                    SetXRawDataParams();
                    TriggerSetXRawDataParams = false;
                }
                if (TriggerVoxelize)
                {
                    Voxelize();
                    TriggerVoxelize = false;
                }
            }
        }

        private void OnRenderObject()
        {
            RenderVoxel();
        }

        private void OnDestroy()
        {
            DeleteResources();
        }

        #endregion

        #region Private Functions
        void InitResources()
        {
            _totalVoxelNum = VoxelNumWidth * VoxelNumHeight * VoxelNumDepth;

            Debug.Log(_totalVoxelNum);

            _targetVoxelDataDeadListBuffer = new ComputeBuffer(_totalVoxelNum, Marshal.SizeOf(typeof(int)), ComputeBufferType.Append);
            _targetVoxelDataDeadListBuffer.SetCounterValue(0);

            _targetVoxelDataBuffer = new ComputeBuffer(_totalVoxelNum, Marshal.SizeOf(typeof(VoxelData)), ComputeBufferType.Default);

            var vd = new VoxelData[_totalVoxelNum];
            for (var i = 0; i < vd.Length; i++)
            {
                vd[i].Position = Random.insideUnitSphere * 5.0f;
                vd[i].Rotation = new Vector4(0, 0, 0, 1);
                vd[i].Color    = new Vector4(1, 1, 1, 0);
                vd[i].Scale    = 0.0f;
            }
            _targetVoxelDataBuffer.SetData(vd);
            vd = null; 
        }
        
        void DeleteResources()
        {
            if (_targetVoxelDataBuffer != null)
            {
                _targetVoxelDataBuffer.Release();
                _targetVoxelDataBuffer = null;
            }

            if (_targetVoxelDataDeadListBuffer != null)
            {
                _targetVoxelDataDeadListBuffer.Release();
                _targetVoxelDataDeadListBuffer = null;
            }

            if (_voxelRenderMat != null)
            {
                if (Application.isEditor)
                {
                    Material.DestroyImmediate(_voxelRenderMat);
                    _voxelRenderMat = null;
                }
                else
                {
                    Material.Destroy(_voxelRenderMat);
                    _voxelRenderMat = null;
                }
            }
        }

        /// <summary>
        /// XRawテクスチャの名前からVoxelサイズ, Textureサイズを抽出
        /// </summary>
        void SetXRawDataParams()
        {
            string xrawDataTexFileName = XRawDataTex.name;
            string data = new Regex(@"\d{1,3}-\d{1,3}-\d{1,3}_\d{1,4}-\d{1,4}").Match(xrawDataTexFileName).ToString();
            //Debug.Log(data);

            string voxelSizeData = data.Split('_')[0];
            string texSizeData   = data.Split('_')[1];

            XRawDataVoxelWidth  = int.Parse(voxelSizeData.Split('-')[0]);
            XRawDataVoxelHeight = int.Parse(voxelSizeData.Split('-')[1]);
            XRawDataVoxelDepth  = int.Parse(voxelSizeData.Split('-')[2]);

            XRawDataTexWidth  = int.Parse(texSizeData.Split('-')[0]);
            XRawDataTexHeight = int.Parse(texSizeData.Split('-')[1]);
        }

        void Voxelize()
        {
            ResetTargetVoxelData();
            AppendTargetVoxelData();
        }

        void ResetTargetVoxelData()
        {
            var id = 0;

            //id = _cs.FindKernel("CSResetTargetVoxelData");
            _targetVoxelDataDeadListBuffer.SetCounterValue(0);
            _cs.SetBuffer(id, "_TargetVoxelDataDeadListBufferAppend", _targetVoxelDataDeadListBuffer);
            _cs.SetBuffer(id, "_TargetVoxelDataBufferWrite", _targetVoxelDataBuffer);
            _cs.Dispatch(id, Mathf.CeilToInt(_totalVoxelNum / 256), 1, 1);
            Debug.Log("ResetTargetVoxelData");
        }

        void AppendTargetVoxelData()
        {
            var id = 1;
            //id = _cs.FindKernel("CSAppendTargetVoxelData");
            var texSize = new Vector4
            (
                XRawDataTexWidth,
                XRawDataTexHeight,
                1.0f / XRawDataTexWidth,
                1.0f / XRawDataTexHeight
            );
            _cs.SetInt("_TotalVoxelNum", _totalVoxelNum);
            _cs.SetFloat("_VoxelScale", VoxelScale);
            _cs.SetFloat("_TotalVoxelGridScale", TotalVoxelGridScale);
            _cs.SetVector("_XRawDataTexSize", texSize);
            _cs.SetVector("_XRawDataVoxelNum", new Vector4(XRawDataVoxelWidth, XRawDataVoxelHeight, XRawDataVoxelDepth, XRawDataVoxelWidth * XRawDataVoxelHeight * XRawDataVoxelDepth));
            _cs.SetBuffer(id, "_TargetVoxelDataBufferWrite", _targetVoxelDataBuffer);
            _cs.SetBuffer(id, "_TargetVoxelDataDeadListBufferConsume", _targetVoxelDataDeadListBuffer);
            _cs.SetTexture(id, "_XRawVoxelDataTex", XRawDataTex);
            _cs.Dispatch(id, Mathf.CeilToInt(XRawDataTexWidth / 8), Mathf.CeilToInt(XRawDataTexHeight / 8), 1);
            Debug.Log("AppendTargetVoxelData");
        }

        void RenderVoxel()
        {
            if (_voxelRenderMat == null)
            {
                _voxelRenderMat = new Material(_voxelRenderShader);
                _voxelRenderMat.hideFlags = HideFlags.DontSave;
            }

            Material m = _voxelRenderMat;

            m.SetPass(0);

            if (Distractor != null)
            {
                m.SetVector("_DistractorPosition", new Vector4(Distractor.localPosition.x, Distractor.localPosition.y, Distractor.localPosition.z, 0.0f));
                m.SetVector("_DistractorRadius", new Vector4(Distractor.localScale.x, 1.0f / Distractor.localScale.x, Mathf.PI / 2.0f, 0.0f));
            }

            m.SetFloat("_VoxelScaleRate", VoxelScaleRate);

            m.SetColor("_Ambient", AmbientColor);
            m.SetFloat("_Ka", Ka);
            //m.SetColor("_Diffuse", DiffuseColor);
            m.SetFloat("_Kd", Kd);
            m.SetColor("_Specular", SpecularColor);
            m.SetFloat("_Ks", Ks);
            m.SetFloat("_Shinness", Shinness);

            m.SetMatrix("_LocalToWorldMatrix", transform.localToWorldMatrix);
            m.SetBuffer("_VoxelDataBuffer", _targetVoxelDataBuffer);

            Graphics.DrawProcedural(MeshTopology.Points, _totalVoxelNum);
        }
        #endregion
    }
}