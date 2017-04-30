using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Assertions;
using System.IO;

namespace irishoak.VoxelizeMagicaVoxelXRawData
{
    /// <summary>
    /// 
    /// </summary>
    public class XRaw2PNGConvertor : EditorWindow
    {
        Object _xrawFileObject = null;
        string _xrawPath = "";
        string _xrawFileName = "";

        // --- .xraw data ---
        int[] _voxelBuffer;
        Color[] _paletteBuffer;

        int _voxelWidth  = 0;
        int _voxelHeight = 0;
        int _voxelDepth  = 0;

        string _magicNumber = "";
        byte _colorChannelDataType;
        byte _numOfColorChannels;
        byte _bitsPerChannel;
        byte _bitsPerIndex;

        int _numOfPaletteColors;

        int _totalVoxelNum;

        int _totalOpaqueVoxelNum;

        // --- output PNG data ---
        string _outputTextureFileName;
        int    _outputTextureWidth  = 1024;
        int    _outputTextureHeight = 1024;
        
        [MenuItem("Tool/irishoak/XRaw2PNGConvertor")]
        static void Open()
        {
            EditorWindow.GetWindow<XRaw2PNGConvertor>("XRaw2PNGConvertor");
        }

        /// <summary>
        /// 
        /// </summary>
        void Create()
        {
            ReadData();
        }

        /// <summary>
        /// 
        /// </summary>
        void ReadData()
        {
            bool isExistFile = File.Exists(Application.dataPath + @"/" + _xrawPath);

            Assert.IsTrue(isExistFile, ".xraw file is note exists.");

            ReadXRawFileData();
            WriteAndSavePNGData();
        }

        /// <summary>
        /// 
        /// </summary>
        void ReadXRawFileData()
        {
            using (FileStream fs = new FileStream(Application.dataPath + @"/" + _xrawPath, FileMode.Open, FileAccess.Read))
            {
                using (BinaryReader br = new BinaryReader(fs))
                {
                    _magicNumber = new string(br.ReadChars(4));
                    _colorChannelDataType = br.ReadByte();
                    _numOfColorChannels = br.ReadByte();
                    _bitsPerChannel = br.ReadByte();
                    _bitsPerIndex = br.ReadByte();
                    _voxelWidth = br.ReadInt32();
                    _voxelHeight = br.ReadInt32();
                    _voxelDepth = br.ReadInt32();
                    _numOfPaletteColors = br.ReadInt32();

                    _voxelBuffer = new int[_voxelWidth * _voxelHeight * _voxelDepth];

                    for (int x = 0; x < _voxelWidth; x++)
                    {
                        for (int y = 0; y < _voxelHeight; y++)
                        {
                            for (int z = 0; z < _voxelDepth; z++)
                            {
                                byte b = br.ReadByte();
                                _voxelBuffer[x + y * _voxelWidth + z * (_voxelWidth * _voxelHeight)] = (int)b;
                            }
                        }
                    }

                    _paletteBuffer = new Color[_numOfPaletteColors];
                    for (int chunk = 0; chunk < _numOfPaletteColors; chunk++)
                    {
                        int r = br.ReadByte();
                        int g = br.ReadByte();
                        int b = br.ReadByte();
                        int a = br.ReadByte();
                        _paletteBuffer[chunk] = new Color(r, g, b, a) / 255.0f;
                    }
                } // using BinaryReader
            } // using FileStream
        }

        /// <summary>
        /// 
        /// </summary>
        void WriteAndSavePNGData()
        {
            // --- Output data as texture ---
            int totalPixelNum = _voxelWidth * _voxelHeight * _voxelDepth;
            _outputTextureWidth  = NearPow2(Mathf.CeilToInt(Mathf.Sqrt(totalPixelNum)));
            _outputTextureHeight = NearPow2(_outputTextureWidth);
            Texture2D outputTex = new Texture2D(_outputTextureWidth, _outputTextureHeight, TextureFormat.ARGB32, false);
            outputTex.filterMode = FilterMode.Point;
            _outputTextureFileName = _xrawFileName + "_" + _voxelWidth + "-" + _voxelHeight + "-" + _voxelDepth + "_" + _outputTextureWidth + "-" + _outputTextureHeight;
            outputTex.name = _outputTextureFileName;
            outputTex.wrapMode = TextureWrapMode.Clamp;

            int idx = 0;
            for (int x = 0; x < _voxelWidth; x++)
            {
                for (int y = 0; y < _voxelHeight; y++)
                {
                    for (int z = 0; z < _voxelDepth; z++)
                    {
                        Color col = _paletteBuffer[_voxelBuffer[x + y * _voxelWidth + z * (_voxelWidth * _voxelHeight)]];

                        int tx = idx % _outputTextureWidth;
                        int ty = Mathf.FloorToInt((float)idx / _outputTextureWidth);
                        outputTex.SetPixel(tx, ty, col);

                        if (Vector3.Magnitude(new Vector3(col.r, col.g, col.b)) > 0.0f)
                        {
                            _totalOpaqueVoxelNum++;
                        }
                        _totalVoxelNum++;
                        idx++;
                    }//z
                }//y
            }//x
            outputTex.Apply();

            byte[] outputPngData = outputTex.EncodeToPNG();

            string filePath = EditorUtility.SaveFilePanel("Save Texture", "", outputTex.name + ".png", "png");

            if (filePath.Length > 0)
            {
                File.WriteAllBytes(filePath, outputPngData);
            }

            DestroyImmediate(outputTex);

            _voxelBuffer   = null;
            _paletteBuffer = null;
        }

        int NearPow2(int n)
        {
            if (n <= 0) return 0;
            if ((n & (n - 1)) == 0) return n;
            int ret = 1;
            while(n > 0) { ret <<= 1; n >>= 1; }
            return ret;
        }

        private void OnGUI()
        {

            GUILayout.Space(8);
            EditorGUILayout.LabelField("> Select .xraw file");

            _xrawFileObject = EditorGUILayout.ObjectField(".xraw Path", _xrawFileObject, typeof(Object), false);

            if (_xrawFileObject != null)
            {
                _xrawPath = AssetDatabase.GetAssetPath(_xrawFileObject);
                _xrawPath = _xrawPath.Replace("Assets/", string.Empty);

                _xrawFileName = Path.GetFileNameWithoutExtension(_xrawPath);
            }

            if (GUILayout.Button("Create"))
            {
                Create();
            }
            
            GUILayout.Space(8);
            EditorGUILayout.LabelField("[.xraw voxel data]");
            EditorGUILayout.TextField("magicNumber", _magicNumber.ToString());
            EditorGUILayout.TextField("colorChannelDataType", _colorChannelDataType.ToString());
            EditorGUILayout.TextField("numOfColorChannels", _numOfColorChannels.ToString());
            EditorGUILayout.TextField("bitsPerChannel", _bitsPerChannel.ToString());
            EditorGUILayout.TextField("voxelDemention.x", _voxelWidth.ToString());
            EditorGUILayout.TextField("voxelDemention.y", _voxelHeight.ToString());
            EditorGUILayout.TextField("voxelDemention.z", _voxelDepth.ToString());
            EditorGUILayout.TextField("bitsPerIndex", _bitsPerIndex.ToString());

            EditorGUILayout.TextField("totalVoxelNum", _totalVoxelNum.ToString());
            EditorGUILayout.TextField("totalOpaqueVoxelNum", _totalOpaqueVoxelNum.ToString());

            GUILayout.Space(8);
            EditorGUILayout.LabelField("[PNG data]");
            _outputTextureFileName = EditorGUILayout.TextField("Output Texture FileName", _outputTextureFileName);

            _outputTextureWidth = EditorGUILayout.IntSlider("Output Texture Width", _outputTextureWidth, 1, 2048);
            _outputTextureHeight = EditorGUILayout.IntSlider("Output Texture Height", _outputTextureHeight, 1, 2048);
            
        }
    }
}