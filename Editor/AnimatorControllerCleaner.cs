using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using YamlDotNet.RepresentationModel;

public static class AnimatorControllerCleaner{
    private const string YamlHeader = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";
    private const string ObjectHeaderPrefix = "--- !u!";

    [MenuItem("Assets/CleanAnimatorControllers")]
    private static void CleanAnimatorContollers(){
        var Assets = Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets);
        if(Assets.Length == 0) return;
        foreach(var Asset in Assets){
            // Debug.Log(Asset);
            var path = AssetDatabase.GetAssetPath(Asset);
            // Debug.Log(path);
            if(path.EndsWith(".controller")){
                var pathArray = new string[] {path};
                AssetDatabase.ForceReserializeAssets(pathArray);
                CleanAnimatorController(path);
            }
        }
        AssetDatabase.Refresh();
    }

    private static void CleanAnimatorController(string path){
        var objList = new List<AnimatorClassObj>();
        // データの取得
        foreach(var obj in ParseAnimator(path)){
            // Debug.Log(obj);
            objList.Add(obj);
        }
        bool[] refArray = new bool[objList.Count];
        for(int i=0; i<objList.Count; ++i){
            refArray[i] = false;
        }

        // AnimatorControllerを起点に、参照されているオブジェクトをチェックする。
        for(int i=0; i<objList.Count; ++i)
        {
            var obj = objList[i];
            if(obj.classID == 91){
                SearchReference(i, ref objList, ref refArray);
            }
        }

        // // refArrayがfalseのままのobjは参照されていない
        // for(int i=0; i<objList.Count; ++i)
        // {
        //     if(!refArray[i]){
        //         var obj = objList[i];
        //         var classID = obj.classID;
        //         var fileID = obj.fileID;
        //         var root = ((YamlMappingNode)obj.node).Children.First();
        //         var rootName = root.Key.ToString();
        //         Debug.Log(
        //             $"obj No.{i} is not referenced.\n"
        //             + $"(classID:{classID} ,fileID:{fileID}, name:{rootName})");
        //     }
        // }

        // refArrayがtrueのオブジェクトのみを書き出し
        var objListFiltered = objList.Where((value, index) => refArray[index]);
        var yaml = PresentAnimator(objListFiltered.ToList());

        File.WriteAllText(path, yaml);
    }


    struct AnimatorClassObj{
        public int classID;
        public string fileID;
        public YamlNode node;
        public string content;

        public AnimatorClassObj(
            int classID, string fileID,
            YamlNode node, string content)
        {
            this.classID = classID;
            this.fileID = fileID;
            this.node = node;
            this.content = content;
        }
    }

    private static void SearchReference(int i, ref List<AnimatorClassObj> objList, ref bool[] refArray)
    {
        if(refArray[i]) return;
        refArray[i] = true;
        AnimatorClassObj obj = objList[i];

        // classID is documented on https://docs.unity3d.com/ja/2019.4/Manual/ClassIDReference.html
        switch(obj.classID)
        {
            case 91:    // AnimatorController
            case 114:   // MonoBehaviour
            case 206:   // BlendTree
            case 1101:  // AnimatorStateTransition
            case 1102:  // AnimatorState
            case 1107:  // AnimatorStateMachine
            case 1109:  // AnimatorTransition
            case 1111:  // AnimatorTransitionBase
                break;
            default:
                Debug.LogWarning("unexcepted classID "+obj.classID);
                throw new System.Exception("unexcepted classID "+obj.classID);
        }
        // obj内のfileID要素を辿る
        foreach(var fileID in RecursiveSearch(obj.node, "fileID")){
            // Debug.Log(fileID);
            if (fileID.GetType() == typeof(string))
            {
                var matchObj = objList.Where(x => x.fileID == (string)fileID);
                if (matchObj.Count() != 0)
                {
                    var j = objList.IndexOf(matchObj.First());
                    SearchReference(j, ref objList, ref refArray);
                }
            }
        }
    }

    public static IEnumerable<string> RecursiveSearch(YamlNode node, string key)
    {
        switch (node)
        {
            case YamlMappingNode mappingNode:
                foreach (var childNode in mappingNode.Children)
                {
                    if (childNode.Key.ToString() == key)
                    {
                        yield return childNode.Value.ToString();
                    }

                    foreach (var result in RecursiveSearch(childNode.Value, key))
                    {
                        yield return result;
                    }
                }
                break;

            case YamlSequenceNode sequenceNode:
                foreach (var childNode in sequenceNode.Children)
                {
                    foreach (var result in RecursiveSearch(childNode, key))
                    {
                        yield return result;
                    }
                }
                break;
        }
    }

    private static int GetClassIDByObjectHeader(string objectHeader)
    {
        return int.Parse(objectHeader.Substring(ObjectHeaderPrefix.Length).Split(' ')[0]);
    }

    private static string GetFileIDByObjectHeader(string objectHeader)
    {
        return objectHeader.Substring(ObjectHeaderPrefix.Length).Split(' ')[1].Substring(1);
    }

    private static IEnumerable<AnimatorClassObj> ParseAnimator(string yamlPath)
    {
        var lines = File.ReadLines(yamlPath);
        var sb = new StringBuilder();
        string objectHeader = null;
        string content = null;
        foreach(var line in lines)
        {
            if(line.StartsWith(ObjectHeaderPrefix))
            {
                if(objectHeader != null){
                    content = sb.ToString();
                    yield return new AnimatorClassObj(
                        GetClassIDByObjectHeader(objectHeader),
                        GetFileIDByObjectHeader(objectHeader),
                        ParseYaml(content),
                        content
                        );
                    sb.Clear();
                }
                objectHeader = line;
                continue;
            }

            // 最初の2行については、まだobjectHeaderが出てきていないので読み飛ばす。
            if(objectHeader != null) sb.Append(line + "\n");
        }
        if(objectHeader == null) yield break;
        content = sb.ToString();
        yield return new AnimatorClassObj(
            GetClassIDByObjectHeader(objectHeader),
            GetFileIDByObjectHeader(objectHeader),
            ParseYaml(content),
            content
            );
    }

    private static YamlNode ParseYaml(string text)
    {
        var yamlStream = new YamlStream();
        var sr = new StringReader(text);
        yamlStream.Load(sr);
        return yamlStream.Documents[0].RootNode;
    }

    private static string PresentYaml(YamlNode node)
    {
        var yamlDocument = new YamlDocument(node);
        var yamlStream = new YamlStream(yamlDocument);
        var sw = new StringWriter();
        sw.NewLine = "\n";
        yamlStream.Save(sw);
        return sw.ToString();
    }

    private static string PresentAnimator(List<AnimatorClassObj> objList)
    {
        var sw = new StringWriter();
        sw.Write(YamlHeader);
        foreach(var obj in objList){
            sw.Write(ObjectHeaderPrefix + obj.classID + " &" + obj.fileID + "\n");
            sw.Write(obj.content);
        }
        return sw.ToString();
    }
}
