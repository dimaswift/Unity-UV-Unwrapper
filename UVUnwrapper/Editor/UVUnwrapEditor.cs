﻿using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using HandyUtilities;

namespace CubePainter.UVUnwrapper
{
    [InitializeOnLoad]
    public class UVUnwrapEditor : EditorWindow
    {

        UVUnwrapData.Side draggedSide;

        Vector2 pressedMousePos, pressedSidePos;

        Texture2D _dummyTexture, _blackTexture, _tmpTexture, _lockIcon;

        public MeshFilter target
        {
            get
            {
                return EditorUtility.InstanceIDToObject(UVUnwrapData.Instance.targetGameObjectID) as MeshFilter;
            }
            set
            {
                if (value)
                    UVUnwrapData.Instance.targetGameObjectID = value.GetInstanceID();
            }
        } 

        public Texture2D lockIcon
        {
            get
            {
                if (_lockIcon == null)
                    _lockIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CubePainter/lock_icon.png");
                return _lockIcon;
            }
        }

        public Texture2D blackTexture
        {
            get
            {
                if (_blackTexture == null)
                {
                    _blackTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                    _blackTexture.SetPixel(0, 0, new Color(0, 0, 0, .25f));
                    _blackTexture.Apply();
                }
                return _blackTexture;
            }
        }

        public Texture2D dummyTexture
        {
            get
            {
                if (_dummyTexture == null)
                {
                    _dummyTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                    _dummyTexture.SetPixel(0, 0, new Color(0, 0, 0, 0));
                    _dummyTexture.Apply();
                }
                return _dummyTexture;
            }
        }

        [MenuItem("Tools/UV Unwrap")]
        static void Open()
        {
            var w = GetWindow<UVUnwrapEditor>("UV Unwrap", true);
            w.minSize = new Vector2(360, 500);
            w.Show();
            HandyEditor.CenterOnMainWin(w);
        }

        void OnEnable()
        {
            Undo.undoRedoPerformed -= OnUndo;
            Undo.undoRedoPerformed += OnUndo;
            SceneView.onSceneGUIDelegate -= OnScene;
            SceneView.onSceneGUIDelegate += OnScene;

        }

        void OnDisable()
        {
            if (UVUnwrapData.Instance.changeScale)
            {
                UVUnwrapData.Instance.changeScale = false;
                HandyEditor.RestoreTool();
            }
              
            EditorUtility.SetDirty(UVUnwrapData.Instance);  
            Undo.undoRedoPerformed -= OnUndo;
            SceneView.onSceneGUIDelegate -= OnScene;
            DestroyImmediate(_tmpTexture);
            DestroyImmediate(dummyTexture);
            DestroyImmediate(blackTexture);
        }

        void OnUndo()
        {
            var data = UVUnwrapData.Instance;
            if (data.autoUpdateTargetUV)
                UpdateTargetUV();
        }

        void OnScene(SceneView view)
        {
            var data = UVUnwrapData.Instance;
            if(data.changeScale && target != null)
            {
                HandyEditor.FreezeScene();
                HandyEditor.HideTools();
                Undo.RecordObject(data, "Scale");
                Undo.RecordObject(target.sharedMesh, "Scale");
                var s = data.scale;
                data.scale = Handles.ScaleHandle(data.scale, target.transform.position, target.transform.rotation, HandleUtility.GetHandleSize(target.transform.position));
                float maxScale = data.targetTexture != null ? data.targetTexture.width / data.pixelScale : 512;
                data.scale.x = Mathf.Clamp(data.scale.x, 1, maxScale);
                data.scale.z = Mathf.Clamp(data.scale.z, 1, maxScale);
                data.scale.y = Mathf.Clamp(data.scale.y, 1, maxScale);
                if (s != data.scale)
                {
                    data.ApplyPixelScale();
                    EditorUtility.SetDirty(data);
                    EditorUtility.SetDirty(target.sharedMesh);
                }
            }
        }

        void OnGUI()
        {
            
            var y = 5f;
            float dockWidth = 350;
            float elementHeight = 25;
            var data = UVUnwrapData.Instance;
            var dockPosX = position.width - dockWidth - 5;
            var gc = GUI.color;
            var winSize = new Vector2(position.width, position.height);

            var container = data.GetScaledTextureRect(data.zoom, new Rect(10, 10, position.width - dockWidth - 20, position.height - 20));
            data.containerRect = container;
            if (data.maximized != maximized)
            {
                data.maximized = maximized;
            }
             

            Undo.RecordObject(data, "UVUnwraper Settings");

            target = (EditorGUI.ObjectField(new Rect(dockPosX, y += elementHeight, dockWidth, 16), target, typeof(MeshFilter), true) as MeshFilter);

            data.showGrid = EditorGUI.Toggle(new Rect(dockPosX, y += elementHeight, dockWidth, 16), "Show Grid", data.showGrid);

            data.textureWidth = EditorGUI.IntField(new Rect(dockPosX, y += elementHeight, dockWidth, 16), "Texture Width", data.textureWidth);
            data.textureHeight = EditorGUI.IntField(new Rect(dockPosX, y += elementHeight, dockWidth, 16), "Texture Height", data.textureHeight);

            data.snapToGrid = EditorGUI.Toggle(new Rect(dockPosX, y += elementHeight, dockWidth, 16), "Snap To Grid", data.snapToGrid);

            data.autoUpdateTargetUV = EditorGUI.Toggle(new Rect(dockPosX, y += elementHeight, dockWidth, 16), "Auto Update UVs", data.autoUpdateTargetUV);
            data.uvChannel = (UVUnwrapData.UVChannel) EditorGUI.EnumPopup(new Rect(dockPosX, y += elementHeight, dockWidth, 16), new GUIContent("UV Channel"), data.uvChannel);
            var tt = data.targetTexture;

            data.targetTexture = EditorGUI.ObjectField(new Rect(dockPosX, y += elementHeight, dockWidth, 16), "Target Texture", data.targetTexture, typeof(Texture2D), true) as Texture2D;
            if (tt != data.targetTexture)
                data.RecalculateGrid();

            data.generateTextureType = (UVUnwrapData.TextureType) EditorGUI.Popup(new Rect(dockPosX + 5 + dockWidth / 2, y += elementHeight, (dockWidth / 2) - 5, 16), (int) data.generateTextureType, System.Enum.GetNames(typeof(UVUnwrapData.TextureType)));
      
            if (GUI.Button(new Rect(dockPosX, y, dockWidth / 2, 16), "Generate  Texture"))
            {
                var texName = string.Format("{0}_{1}x{2}", target != null ? target.name + "_texture" : "texture", data.textureWidth, data.textureHeight);
                var path = EditorUtility.SaveFilePanelInProject("Save texture", texName, "png", data.texturePath, data.texturePath);
                if (!string.IsNullOrEmpty(path))
                {
                    data.texturePath = path;
                    var t = UVUnwrapData.GenerateTexture(data.textureWidth, data.textureHeight, data.generateTextureType, data.color1, data.color2);
               
                    System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
                    AssetDatabase.ImportAsset(path);
                    var imp = (TextureImporter) AssetImporter.GetAtPath(path);
                    imp.isReadable = true;
                    imp.filterMode = FilterMode.Point;
                    imp.textureType = TextureImporterType.Advanced;
                    imp.textureFormat = TextureImporterFormat.ARGB32;
                    imp.mipmapEnabled = false;
                    imp.SaveAndReimport();
                    data.targetTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }
            data.color1 = EditorGUI.ColorField(new Rect(dockPosX, y += elementHeight, (dockWidth / 2) - 5, 16), data.color1);
            data.color2 = EditorGUI.ColorField(new Rect(dockPosX + 5 + dockWidth / 2, y, (dockWidth / 2) - 5, 16), data.color2);
            if (target != null)
            {
                if (GUI.Button(new Rect(dockPosX, y += elementHeight, dockWidth, 16), "Generate Mesh"))
                {
                    var path = EditorUtility.SaveFilePanelInProject("Save mesh", target.name + "_mesh", "asset", data.meshPath, data.meshPath);
                    if (!string.IsNullOrEmpty(path))
                    {
                        data.meshPath = path;
                        GenerateMesh(target, path);
                    }
                }
                if (GUI.Button(new Rect(dockPosX, y += elementHeight, dockWidth, 16), "Update UVs"))
                {
                    UpdateTargetUV();
                }
                if (GUI.Button(new Rect(dockPosX, y += elementHeight, dockWidth, 16), "Save Prefab"))
                {
                    var path = EditorUtility.SaveFilePanelInProject("Enter prefab name", target.name, "prefab", "", data.prefabPath);
                    if (!string.IsNullOrEmpty(path))
                    {

                        data.prefabPath = path;
                        CreatePrefab(Helper.ConvertLoRelativePath( new System.IO.FileInfo(path).Directory.FullName), System.IO.Path.GetFileNameWithoutExtension(path), data.createFolder, data.targetTexture);
                    }
                }
                data.createFolder = EditorGUI.Toggle(new Rect(dockPosX, y += elementHeight, dockWidth, 16), "Create Folder", data.createFolder);

            }
            var cs = data.changeScale;
            data.changeScale = GUI.Toggle(new Rect(dockPosX, y += elementHeight, dockWidth, 16), data.changeScale, "Change Scale", EditorStyles.toolbarButton);
            if(data.changeScale != cs && data.changeScale == false)
            {
                HandyEditor.RestoreTool();
            }
            var s = data.pixelScale;
            data.pixelScale = EditorGUI.Slider(new Rect(dockPosX, y += elementHeight, dockWidth, 16), data.pixelScale, 0.00001f, 0.1f);
            if(s != data.pixelScale && target != null)
            {
                Undo.RecordObject(target.sharedMesh, "Scale");
                data.ApplyPixelScale();
                EditorUtility.SetDirty(target);  
            }
            y += elementHeight;
            for (int i = 0; i < data.sides.Count; i++)
            {
                var side = data.sides[i];
                GUI.color = gc;
                side.locked = EditorGUI.Toggle(new Rect(dockPosX + dockWidth - 50, y, 16, 16), side.locked);
                GUI.DrawTexture(new Rect(dockPosX + dockWidth - 35, y, 16, 16), lockIcon);
                GUI.color = side == draggedSide ? side.color * 3 : side.color;
                GUI.Box(new Rect(dockPosX, y, 70, 20), blackTexture);
                GUI.color = side.locked ? gc.SetAlpha(.2f) : gc;
                EditorGUI.LabelField(new Rect(dockPosX, y, 70, 16), side.name, new GUIStyle() { alignment = TextAnchor.MiddleCenter });
               // EditorGUI.LabelField(new Rect(dockPosX + 75, y, 150, 16), string.Format("x:{0:0.000}, y:{1:0.000}", side.uvOrigin.x, side.uvOrigin.y));
                var _si = side.showInfo;
                side.showInfo = EditorGUI.Foldout(new Rect(dockPosX + dockWidth - 20, y, 20, 16), side.showInfo, "", true);
                if (side.showInfo && side.showInfo != _si)
                    SwitchSide(side); 
                if (GUI.Button(new Rect(dockPosX + 80, y, 70, 16), "Rotate UV"))
                {
                    side.RotateUV();
                    UpdateTargetUV();
                }
                if (GUI.Button(new Rect(dockPosX + 155, y, 80, 16), "Rotate Rect"))
                {
                    side.RotateRect();
                    UpdateTargetUV();
                }
                if (GUI.Button(new Rect(dockPosX + 240, y, 50, 16), "Reflect"))
                {
                    side.mirrored = !side.mirrored;
                    UpdateTargetUV();
                }

                if (side.showInfo)
                {
                    EditorGUI.LabelField(new Rect(dockPosX, y += elementHeight, dockWidth, 16), "UVS:", EditorStyles.centeredGreyMiniLabel);
                    for (int j = 0; j < 4; j++)
                    {
                        EditorGUI.Vector2Field(new Rect(dockPosX, y += elementHeight, dockWidth, 16), "", side.uvs[j]);
                    }
                    EditorGUI.LabelField(new Rect(dockPosX, y += elementHeight, dockWidth, 16), "Rect:", EditorStyles.centeredGreyMiniLabel);
                    EditorGUI.RectField(new Rect(dockPosX, y += elementHeight, dockWidth, 16), side.rect);
                    y += elementHeight;
                }
                y += elementHeight;
            }
            GUI.color = gc;

            var e = Event.current;
            if(position.width > 500)
            {
                if (data.targetTexture != null)
                {
                    GUI.DrawTexture(container, data.targetTexture);
                }

                if (draggedSide != null)
                {
                    y += elementHeight;
                    //  GUI.DrawTexture(new Rect(width - _tmpTexture.width, y, _tmpTexture.width, _tmpTexture.height), _tmpTexture, ScaleMode.ScaleToFit);
                }

                if (e.button == 0)
                {
                    if (e.type == EventType.MouseDown && container.Contains(e.mousePosition))
                    {
                        pressedMousePos = e.mousePosition;
                        bool touchedSide = false;
                        foreach (var side in data.sides)
                        {
                            if (!side.locked && side.Contains(pressedMousePos))
                            {
                                touchedSide = true;
                                SwitchSide(side);
                                pressedSidePos = side.rect.position;
                                break;
                            }
                        }
                        if (!touchedSide)
                            draggedSide = null;
                    }
                    if (e.type == EventType.MouseDrag && draggedSide != null && container.Contains(e.mousePosition))
                    {
                        var r = draggedSide.rect;
                        var pos = pressedSidePos + (e.mousePosition - pressedMousePos);
                        pos.x = Mathf.Clamp(pos.x, container.x, container.x + container.width - r.width);
                        pos.y = Mathf.Clamp(pos.y, container.y, container.y + container.height - r.height);
                        if (data.snapToGrid && data.poinsPerPixel > 3)
                        {
                            pos.x = data.grid.GetClosetColumn(pos.x);
                            pos.y = data.grid.GetClosetRow(pos.y);
                        }
                        r.position = pos;
                        draggedSide.rect = r;

                        draggedSide.MapPositionFromRect(container);
                        if (data.autoUpdateTargetUV)
                            UpdateTargetUV();
                        //DrawSidePreview();
                    }
                }
                if (draggedSide != null)
                {
                    GUI.color = new Color(0, 0, 0, 1);
                    DrawRectagle(draggedSide.rect, 2);
                    GUI.color = gc;
                }
                if (data.poinsPerPixel > 3 && data.showGrid)
                {
                    for (int i = 0; i < data.grid.rowCount; i++)
                    {
                        GUI.DrawTexture(new Rect(container.x, data.grid.GetRow(i), container.width, 1), blackTexture, ScaleMode.StretchToFill);
                    }
                    for (int i = 0; i < data.grid.columnCount; i++)
                    {
                        GUI.DrawTexture(new Rect(data.grid.GetColumn(i), container.y, 1, container.height), blackTexture, ScaleMode.StretchToFill);
                    }

                    GUI.DrawTexture(new Rect(container.x, container.y + container.height, container.width, 1), blackTexture, ScaleMode.StretchToFill);
                    GUI.DrawTexture(new Rect(container.x + container.width, container.y, 1, container.height), blackTexture, ScaleMode.StretchToFill);

                }
                else
                {
                    DrawRectagle(container);
                }

                for (int i = 0; i < data.sides.Count; i++)
                {
                    var side = data.sides[i];

                    GUI.color = side.color;
                    GUI.Box(side.rect, dummyTexture);
                    GUI.color = gc;
                    GUI.Label(new Rect((side.rect.x + (side.rect.width / 2)) - 50, (side.rect.y + (side.rect.height / 2)) - 50, 100, 100), side.name, new GUIStyle() { alignment = TextAnchor.MiddleCenter });
                    if (side.locked)
                    {
                        var lockWidth = side.rect.width > side.rect.height ? side.rect.width / 3 : side.rect.height / 3;
                        GUI.DrawTexture(new Rect(side.rect.center.x - lockWidth / 2, side.rect.center.y - lockWidth / 2, lockWidth, lockWidth), lockIcon);
                    }
                }
            }
          

            if (data.winSize != winSize)
            {
                data.winSize = winSize;
                data.RecalculateGrid();
                data.BindSidesToContainer();
                if (data.autoUpdateTargetUV)
                    UpdateTargetUV();
            }

         
            EditorUtility.SetDirty(data);
            Repaint();
        }


        public void UpdateTargetUV()
        {
            if(target)
            {
                var mesh = target.sharedMesh;
                if(UVUnwrapData.Instance.uvChannel == UVUnwrapData.UVChannel.UVChannle1)
                    mesh.uv = UVUnwrapData.Instance.GenerateUV();
                else mesh.uv2 = UVUnwrapData.Instance.GenerateUV();
                EditorUtility.SetDirty(mesh);
            }

        }

        public void CreatePrefab(string assetFolder, string name, bool createFolder, Texture2D texture)
        {
            var data = UVUnwrapData.Instance;
            var prefab = Instantiate(target);
            prefab.name = target.name;
            var mesh = Instantiate(target.sharedMesh);
            mesh.name = prefab.name + "_mesh";
            var mat = Instantiate(target.GetComponent<MeshRenderer>().sharedMaterial);
            mat.name = prefab.name + "_mat";
            mesh.uv = data.GenerateUV();
            prefab.sharedMesh = mesh;
            if (createFolder)
            {
                AssetDatabase.CreateFolder(assetFolder, name);
                assetFolder += "/" + name.ToUpperInvariant(); 
            }
            var meshPath = assetFolder + "/" + name + "_mesh.asset";
            var prefabPath = assetFolder + "/" + name + ".prefab";
            var matPath = assetFolder + "/" + name + "_mat.mat";
            var texPath = assetFolder + "/" + name + "_decal.png";

          
            PrefabUtility.CreatePrefab(prefabPath, prefab.gameObject);

            HandyEditor.MakeTextureReadable(texture);
         //   System.IO.File.WriteAllBytes(Application.dataPath + texPath.Remove(0, 6), texture.EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
            var t = Instantiate(texture);
            t.name = prefab.name + "_decal";
            mat.SetTexture("_Decal", t);
            AssetDatabase.AddObjectToAsset(mesh, prefabPath);
            AssetDatabase.AddObjectToAsset(mat, prefabPath);
            AssetDatabase.AddObjectToAsset(t, prefabPath);
            AssetDatabase.ImportAsset(prefabPath);
            EditorUtility.SetDirty(prefab);  
            EditorUtility.SetDirty(mesh);
           
            

            DestroyImmediate(prefab.gameObject);

            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<MeshFilter>();
            prefab.sharedMesh = mesh;
            prefab.GetComponent<MeshRenderer>().sharedMaterial = mat;
     
            target = PrefabUtility.ConnectGameObjectToPrefab(target.gameObject, prefab.gameObject).GetComponent<MeshFilter>();
            Selection.activeGameObject = target.gameObject;
        }

        void GenerateMesh(MeshFilter target, string path)
        {
            var mesh = Instantiate(target.sharedMesh);
            mesh.uv = UVUnwrapData.Instance.GenerateUV();
            var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            List<MeshFilter> filtersToReplace = new List<MeshFilter>();
            if (existingMesh != null)
            {
                var filters = FindObjectsOfType<MeshFilter>();
                foreach (var mf in filters)
                {
                    if (mf.sharedMesh == existingMesh)
                    {
                        filtersToReplace.Add(mf);
                    }
                }
            }
            AssetDatabase.CreateAsset(mesh, path);
            foreach (var mf in filtersToReplace)
            {
                mf.sharedMesh = mesh;
            }
        }

        void DrawSidePreview()
        {
            if (draggedSide != null && UVUnwrapData.Instance.targetTexture != null)
            {
                var targetTex = UVUnwrapData.Instance.targetTexture;
                var offset = new Vector2(draggedSide.uvs[0].x * targetTex.width, draggedSide.uvs[0].y * targetTex.height);
                for (int x = 0; x < _tmpTexture.width; x++)
                {
                    for (int y = 0; y < _tmpTexture.height; y++)
                    {
                        _tmpTexture.SetPixel(x, y, targetTex.GetPixel((int) (offset.x + x), (int) (offset.y + y)));
                    }
                }
                _tmpTexture.Apply();
            }
        }

        void SwitchSide(UVUnwrapData.Side side)
        {
            if (draggedSide != side)
            {
                var settings = UVUnwrapData.Instance;
                draggedSide = side;
                DestroyImmediate(_tmpTexture);
                if (settings.targetTexture != null)
                {
                    _tmpTexture = new Texture2D((int) (settings.targetTexture.width * draggedSide.size.x), (int) (settings.targetTexture.height * draggedSide.size.y), TextureFormat.ARGB32, false);
                  //  DrawSidePreview();
                }
            }

        }

        public void DrawRectagle(Rect rect, float width = 1f)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height, rect.width, width), blackTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(rect.x, rect.y + width, width, rect.height), blackTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, width), blackTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(rect.x + rect.width, rect.y, width, rect.height), blackTexture, ScaleMode.StretchToFill);
        }

        class SidePanel
        {
            Color color;
            int index;
        }
    }

}
