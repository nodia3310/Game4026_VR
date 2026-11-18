using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game4026.Interaction.Grabbing;
using Game4026.Interaction.Melee;
using Game4026.Interaction.TestScene;

// Editor API migration for the supplied FBX files; source models are left untouched.
public static class ReplaceInteractionModels
{
    const string Source="Assets/Weapon-20261111T153418Z-1-001 1/Weapon/SM_ORPH_";
    const string Destination="Assets/_Game/InteractionTest/Materials/";
    static Material steel, rubber, wood, painted, lens;
    public static string Main()
    {
        var scene=SceneManager.GetActiveScene();
        if(Application.isPlaying || scene.path!="Assets/_Game/Scenes/InteractionTest.unity" || scene.isDirty)
            throw new InvalidOperationException("Open the saved InteractionTest scene in Edit mode first.");
        var primary=UnityEngine.Object.FindAnyObjectByType<MeleeWeapon>();
        if(!primary || primary.transform.Find("Imported Model")) throw new InvalidOperationException("Already migrated or no original weapon. Inspect before editing.");
        steel=Material("Imported Steel",new Color(.47f,.52f,.57f),.7f,.45f);
        rubber=Material("Imported Grip",new Color(.055f,.065f,.07f),0,.25f);
        wood=Material("Imported Wood",new Color(.32f,.16f,.065f),0,.28f);
        painted=Material("Imported Painted Metal",new Color(.34f,.09f,.055f),.45f,.3f);
        lens=Material("Imported Lens",new Color(.82f,.92f,1),.25f,.8f);
        var variants=new MeleeWeapon[4];variants[0]=primary;
        for(int i=1;i<4;i++)variants[i]=UnityEngine.Object.Instantiate(primary.gameObject).GetComponent<MeleeWeapon>();
        string[] kinds={"Knife","Axe","Crowbar","Shovel"};
        float[] masses={.35f,1.15f,.9f,1.1f};
        float[] gripY={.06f,.09f,.09f,.09f};
        // Endpoints are on the visible cutting/striking portion, in the supplied model's native coordinates.
        Vector3[] bases={new Vector3(0,.135f,0),new Vector3(0,.49f,.13f),new Vector3(0,.48f,.075f),new Vector3(0,.40f,.01f)};
        Vector3[] tips={new Vector3(0,.335f,0),new Vector3(0,.625f,.13f),new Vector3(0,.615f,.075f),new Vector3(0,.64f,.01f)};
        for(int i=0;i<4;i++)
        {
            var weapon=variants[i];weapon.name="Melee weapon - "+kinds[i];
            foreach(string name in new[]{"Grip","Blade","Guard"})
            {
                var child=weapon.transform.Find(name);if(child)UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            var model=Model(kinds[i],weapon.transform,Quaternion.Euler(90,0,0),new Vector3(0,0,-gripY[i]));
            foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                string n=renderer.name;
                var material=steel;
                if(kinds[i]=="Knife" && n=="knife3")material=rubber;
                if(kinds[i]=="Axe" && n=="readyaxe2")material=wood;
                if(kinds[i]=="Crowbar")material=painted;
                if(kinds[i]=="Shovel" && n=="trenchshovel14")material=wood;
                renderer.sharedMaterial=material;
                FitBox(renderer);
            }
            var bladeBase=weapon.transform.Find("Blade Base");var bladeTip=weapon.transform.Find("Blade Tip");
            bladeBase.localPosition=Quaternion.Euler(90,0,0)*bases[i]+model.transform.localPosition;
            bladeTip.localPosition=Quaternion.Euler(90,0,0)*tips[i]+model.transform.localPosition;
            var physical=weapon.GetComponent<PhysicalGrabbable>();Set(physical,"massKg",masses[i]);Set(weapon,"effectiveMass",masses[i]);
            weapon.transform.SetPositionAndRotation(new Vector3(-4.15f+i*.85f,.99f,.53f),Quaternion.identity);
            Rebind(physical);
        }
        var feedback=UnityEngine.Object.FindAnyObjectByType<InteractionTestFeedback>();
        Set(feedback,"melee",variants[0]);SetArray(feedback,"additionalMelee",variants.Skip(1).Cast<UnityEngine.Object>().ToArray());
        var desktop=UnityEngine.Object.FindAnyObjectByType<InteractionDesktopMode>();Set(desktop,"melee",variants[0]);
        var flashlight=UnityEngine.Object.FindAnyObjectByType<GrabbableFlashlight>();
        foreach(string name in new[]{"Torch body","Lens"})
        {
            var child=flashlight.transform.Find(name);if(child)UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        var torchModel=Model("Flashlight",flashlight.transform,Quaternion.identity,new Vector3(0,-.03f,0));
        foreach(var renderer in torchModel.GetComponentsInChildren<MeshRenderer>())
        {
            renderer.sharedMaterial=renderer.name=="flashlight1"?lens:renderer.name=="flashlight6"?rubber:steel;
            FitBox(renderer);
        }
        flashlight.Beam.transform.localPosition=new Vector3(0,0,.112f);flashlight.Beam.transform.localRotation=Quaternion.identity;
        flashlight.transform.SetPositionAndRotation(new Vector3(-.65f,.9f,.65f),Quaternion.identity);
        Rebind(flashlight.GetComponent<PhysicalGrabbable>());
        var bench=GameObject.Find("Equipment bench");bench.transform.position=new Vector3(-1.5f,.72f,.65f);bench.transform.localScale=new Vector3(6.6f,.16f,.8f);
        var leg=GameObject.Find("Bench leg");var extraLeg=UnityEngine.Object.Instantiate(leg,leg.transform.parent);extraLeg.name="Bench leg - weapon extension";extraLeg.transform.position=new Vector3(-4.35f,.34f,.65f);
        var labels=GameObject.Find("Bench labels");labels.GetComponent<TextMesh>().text="LIGHT";labels.transform.position=new Vector3(-.65f,1f,.25f);
        var rayLabel=UnityEngine.Object.Instantiate(labels,labels.transform.parent);rayLabel.name="Ray tool label";rayLabel.transform.position=new Vector3(.38f,1f,.25f);rayLabel.GetComponent<TextMesh>().text="RAY";
        var bowLabel=UnityEngine.Object.Instantiate(labels,labels.transform.parent);bowLabel.name="Bow label";bowLabel.transform.position=new Vector3(1.2f,1f,.25f);bowLabel.GetComponent<TextMesh>().text="BOW";
        var title=GameObject.Find("Melee sign").GetComponent<TextMesh>();title.text="01 / MELEE x4\nKnife / Axe / Crowbar / Shovel";
        for(int i=0;i<4;i++)
        {
            var label=UnityEngine.Object.Instantiate(labels,labels.transform.parent);label.name="Weapon label - "+kinds[i];label.transform.position=new Vector3(-4.15f+i*.85f,1f,.22f);label.GetComponent<TextMesh>().text=kinds[i].ToUpperInvariant();
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return "Replaced flashlight and added Knife, Axe, Crowbar, Shovel; 8 grabbables, 4 melee event sources. Source FBX/material assets unchanged.";
    }
    static GameObject Model(string kind,Transform parent,Quaternion rotation,Vector3 position)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Source+kind+".fbx");if(!asset)throw new Exception("Missing "+kind);
        var model=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);model.name="Imported Model";
        model.transform.localPosition=position;model.transform.localRotation=rotation;model.transform.localScale=Vector3.one;
        // FBX export-container translation is not part of the tool geometry or grip pose.
        var exportRoot=model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="T_ORPH_ToolsAll");
        if(exportRoot){exportRoot.localPosition=Vector3.zero;PrefabUtility.RecordPrefabInstancePropertyModifications(exportRoot);}
        foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=parent.gameObject.layer;
        return model;
    }
    static void FitBox(MeshRenderer renderer)
    {
        var bounds=renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
        var box=renderer.gameObject.AddComponent<BoxCollider>();box.center=bounds.center;
        box.size=new Vector3(Mathf.Max(.012f,bounds.size.x),Mathf.Max(.012f,bounds.size.y),Mathf.Max(.012f,bounds.size.z));
    }
    static void Rebind(PhysicalGrabbable physical)
    {
        physical.Grab.colliders.Clear();physical.Grab.colliders.AddRange(physical.GetComponentsInChildren<Collider>().Where(c=>!c.isTrigger));physical.ApplyPhysicsSettings();
    }
    static Material Material(string name,Color color,float metallic,float smoothness)
    {
        var path=Destination+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smoothness);return m;
    }
    static void Set(UnityEngine.Object target,string field,object value)
    {
        var so=new SerializedObject(target);var p=so.FindProperty(field);if(p==null)throw new Exception(field);
        if(value is UnityEngine.Object obj)p.objectReferenceValue=obj;else if(value is float f)p.floatValue=f;
        so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(target);
    }
    static void SetArray(UnityEngine.Object target,string field,UnityEngine.Object[] values)
    {
        var so=new SerializedObject(target);var p=so.FindProperty(field);p.arraySize=values.Length;
        for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();
    }
}
