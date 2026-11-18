using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Game4026.Contracts;
using Game4026.Interaction.Grabbing;
using Game4026.Interaction.Melee;
using Game4026.Interaction.Ranged;
using Game4026.Interaction.Artifacts;
using Game4026.Interaction.TestScene;

// Run through Unity CLI script run. Never overwrites an existing scene.
public static class BuildInteractionTest
{
    public const string ScenePath = "Assets/_Game/Scenes/InteractionTest.unity";
    const string Art = "Assets/_Game/InteractionTest";
    static Material gray, dark, blue, gold, white, lineMat;
    static int targetLayer, propLayer, artifactLayer;
    static Transform root;

    public static string Main()
    {
        if (File.Exists(ScenePath)) throw new InvalidOperationException("Scene already exists. Inspect/edit it; do not regenerate over user content.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("An open scene is dirty; preserve it before building.");
        Directory.CreateDirectory("Assets/_Game/Scenes"); Directory.CreateDirectory(Art + "/Materials"); Directory.CreateDirectory(Art + "/Prefabs");
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        targetLayer = Layer("InteractionTestTarget"); propLayer = Layer("InteractionTestProp"); artifactLayer = Layer("InteractionTestArtifact");
        gray = Mat("Gray", new Color(.42f,.46f,.49f)); dark = Mat("Dark", new Color(.075f,.10f,.13f));
        blue = Mat("Blue", new Color(.08f,.48f,.64f)); gold = Mat("Gold", new Color(.85f,.53f,.16f));
        white = Mat("White", new Color(.85f,.87f,.82f)); lineMat = Mat("Line", new Color(.1f,1f,.85f), true);
        root = new GameObject("INTERACTION TEST - graybox").transform;
        Box("Floor", new Vector3(0,-.1f,3), new Vector3(12,.2f,14), gray);
        Box("Backstop", new Vector3(0,2.2f,9), new Vector3(12,4.4f,.2f), dark);
        Box("Left wall", new Vector3(-6,1.5f,3), new Vector3(.2f,3,12), gray);
        Box("Right wall", new Vector3(6,1.5f,3), new Vector3(.2f,3,12), gray);
        var sun = new GameObject("Room key light").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = .65f;
        sun.transform.rotation = Quaternion.Euler(48,-35,0); sun.shadows = LightShadows.Soft;
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.24f,.28f,.32f);
        RenderSettings.skybox = null;
        var manager = new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab"));
        rig.transform.position = new Vector3(0,0,-1.5f);
        var cam = rig.GetComponentInChildren<Camera>(true); cam.backgroundColor = new Color(.055f,.075f,.10f); cam.clearFlags = CameraClearFlags.SolidColor;
        foreach (var interactor in rig.GetComponentsInChildren<XRBaseInteractor>(true)) interactor.interactionManager = manager;
        foreach (var component in rig.GetComponentsInChildren<Component>(true))
        {
            if (component.GetType().Name == "SphereInteractionCaster")
            {
                Set(component,"m_PhysicsLayerMask",1 | (1<<propLayer) | (1<<artifactLayer));
                Set(component,"m_PhysicsTriggerInteraction",(int)QueryTriggerInteraction.Collide);
            }
            if (component.GetType().Name == "CurveInteractionCaster") Set(component,"m_RaycastMask",1 | (1<<targetLayer) | (1<<propLayer) | (1<<artifactLayer));
        }
        // Existing Starter Assets actions/prefab variants retain their left/right bindings.
        var services = new GameObject("Test Room Services");
        var pipelinePath = Art + "/InteractionTest URP.asset";
        if (!AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath))
            AssetDatabase.CopyAsset("Assets/Settings/Project Configuration/Performance URP Config.asset", pipelinePath);
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        Set(pipeline, "m_AdditionalLightsRenderingMode", 1); Set(pipeline,"m_AdditionalLightsPerObjectLimit", 1);
        Set(services.AddComponent<SceneLightingOverride>(), "testPipeline", pipeline);

        // Nearby bench and four physically grabbable tools.
        Box("Equipment bench",new Vector3(0,.72f,.65f),new Vector3(3.6f,.16f,.8f),dark);
        foreach(float x in new[]{-1.5f,1.5f}) Box("Bench leg",new Vector3(x,.34f,.65f),new Vector3(.12f,.68f,.6f),gray);
        var sword = Item("Melee weapon",new Vector3(-1.15f,.91f,.6f), .8f);
        Box("Grip",Vector3.zero,new Vector3(.08f,.08f,.24f),dark,sword.transform);
        Box("Blade",new Vector3(0,0,.47f),new Vector3(.065f,.035f,.7f),white,sword.transform);
        Box("Guard",new Vector3(0,0,.14f),new Vector3(.28f,.05f,.05f),gold,sword.transform);
        var melee = sword.AddComponent<MeleeWeapon>();
        Set(melee,"bladeBase",Marker(sword.transform,"Blade Base",new Vector3(0,0,.15f)));
        var tip = Marker(sword.transform,"Blade Tip",new Vector3(0,0,.82f)); Set(melee,"bladeTip",tip);
        Set(melee,"rigMotionReference",rig.transform); Set(melee,"obstructionOrigin",cam.transform); Set(melee,"ignoredPlayerRoot",rig.transform);
        Set(melee,"targetMask",1 << targetLayer); Set(melee,"blockerMask",(1 << targetLayer) | 1);
        var trail = tip.gameObject.AddComponent<TrailRenderer>(); trail.sharedMaterial = lineMat; trail.time = .2f; trail.startWidth = .018f; trail.endWidth = .002f; trail.minVertexDistance = .015f;
        FinishItem(sword);

        var torch = Item("Flashlight",new Vector3(-.38f,.94f,.65f),.35f);
        Box("Torch body",Vector3.zero,new Vector3(.10f,.10f,.27f),dark,torch.transform);
        Box("Lens",new Vector3(0,0,.16f),new Vector3(.13f,.13f,.04f),white,torch.transform);
        var beam = Marker(torch.transform,"Spot beam",new Vector3(0,0,.19f)).gameObject.AddComponent<Light>();
        beam.type = LightType.Spot; beam.intensity = 9f; beam.range = 14f; beam.spotAngle = 42f; beam.innerSpotAngle = 24f; beam.color = new Color(1f,.93f,.78f); beam.shadows = LightShadows.None;
        var flashlight = torch.AddComponent<GrabbableFlashlight>(); Set(flashlight,"beam",beam); FinishItem(torch);

        var gun = Item("Ray tool",new Vector3(.38f,.94f,.65f),.5f);
        Box("Ray body",Vector3.zero,new Vector3(.12f,.12f,.28f),blue,gun.transform);
        Box("Muzzle",new Vector3(0,0,.19f),new Vector3(.07f,.07f,.12f),dark,gun.transform);
        var ray = gun.AddComponent<RayAim>(); Set(ray,"rayOrigin",Marker(gun.transform,"Ray Origin",new Vector3(0,0,.27f)));
        Set(ray,"ignoreHierarchy",gun.transform); Set(ray,"hitMask",1 | (1 << targetLayer)); Set(ray,"aimLine",Line(gun.transform,"Aim ray",.009f)); FinishItem(gun);

        var arrow = new GameObject("Test Arrow"); arrow.layer=propLayer;
        Box("Shaft",new Vector3(0,0,-.20f),new Vector3(.022f,.022f,.40f),white,arrow.transform);
        Box("Arrowhead",Vector3.zero,new Vector3(.045f,.045f,.08f),gold,arrow.transform);
        var arrowBody=arrow.AddComponent<Rigidbody>(); arrowBody.mass=.06f;
        var projectile=arrow.AddComponent<ArrowProjectile>(); Set(arrow.AddComponent<ProjectileImpactFeedback>(),"impactMaterial",lineMat);
        var arrowPrefab=PrefabUtility.SaveAsPrefabAsset(arrow,Art+"/Prefabs/TestArrow.prefab").GetComponent<ArrowProjectile>(); UnityEngine.Object.DestroyImmediate(arrow);

        var bowObj = Item("Two hand bow",new Vector3(1.2f,1.42f,.65f),.6f);
        Box("Bow grip",Vector3.zero,new Vector3(.08f,.24f,.08f),dark,bowObj.transform);
        Box("Upper limb",new Vector3(0,.32f,.08f),new Vector3(.065f,.48f,.07f),gold,bowObj.transform);
        Box("Lower limb",new Vector3(0,-.32f,.08f),new Vector3(.065f,.48f,.07f),gold,bowObj.transform);
        // Static cradle catches the bow without constraining its Rigidbody.
        Box("Bow cradle",new Vector3(1.2f,.84f,.73f),new Vector3(.38f,.08f,.34f),blue);
        var rest=Marker(bowObj.transform,"String Rest",new Vector3(0,0,-.14f));
        var pull=Marker(bowObj.transform,"String Visual",rest.localPosition);
        var stringObj=new GameObject("String Interactable"); stringObj.transform.SetParent(bowObj.transform,false); stringObj.transform.localPosition=rest.localPosition;
        var stringCol=stringObj.AddComponent<SphereCollider>(); stringCol.isTrigger=true; stringCol.radius=.065f;
        var stringInteract=stringObj.AddComponent<XRSimpleInteractable>(); stringInteract.selectMode=InteractableSelectMode.Single; stringInteract.colliders.Add(stringCol); stringInteract.interactionManager=manager;
        Box("String bead",Vector3.zero,new Vector3(.045f,.045f,.045f),blue,pull,false);
        var bow=bowObj.AddComponent<BowDrawAim>(); Set(bow,"stringInteractable",stringInteract); Set(bow,"stringRest",rest); Set(bow,"stringVisual",pull);
        Set(bow,"arrowOrigin",Marker(bowObj.transform,"Arrow Origin",new Vector3(0,0,.20f))); Set(bow,"arrowPrefab",arrowPrefab); Set(bow,"trajectoryLine",Line(bowObj.transform,"Trajectory",.008f));
        SetArray(bow,"additionalIgnoredColliders",rig.GetComponentsInChildren<Collider>(true));
        var stringLine=Line(bowObj.transform,"Visible bow string",.008f); var stringVisual=stringLine.gameObject.AddComponent<BowStringVisual>();
        Set(stringVisual,"upper",Marker(bowObj.transform,"Upper string anchor",new Vector3(0,.56f,.08f))); Set(stringVisual,"lower",Marker(bowObj.transform,"Lower string anchor",new Vector3(0,-.56f,.08f))); Set(stringVisual,"pullPoint",pull); Set(stringVisual,"line",stringLine);
        FinishItem(bowObj);

        var jar=Item("Artifact jar - interaction-jar-01",new Vector3(2.8f,1.13f,.8f),.75f);
        Box("Jar body",Vector3.zero,new Vector3(.23f,.30f,.23f),gold,jar.transform);
        Box("Jar lid",new Vector3(0,.17f,0),new Vector3(.27f,.04f,.27f),white,jar.transform);
        foreach(var tr in jar.GetComponentsInChildren<Transform>()) tr.gameObject.layer=artifactLayer;
        jar.AddComponent<ArtifactIdentity>().Configure("interaction-jar-01",ArtifactType.CanopicJar); jar.AddComponent<RestorableArtifact>();
        Set(jar.GetComponent<PhysicalGrabbable>(),"useDynamicGrip",true); FinishItem(jar);
        Box("Artifact pickup pedestal",new Vector3(2.8f,.48f,.8f),new Vector3(.65f,.96f,.65f),dark);
        Box("Display cabinet plinth",new Vector3(2.8f,.48f,2.6f),new Vector3(.9f,.96f,.9f),dark);
        Box("Display tray",new Vector3(2.8f,.98f,2.6f),new Vector3(.85f,.04f,.85f),blue);
        for(int x=-1;x<=1;x+=2) Box("Cabinet rear upright",new Vector3(2.8f+x*.42f,1.3f,2.99f),new Vector3(.035f,.64f,.035f),gray);
        Box("Cabinet rear panel",new Vector3(2.8f,1.3f,3.02f),new Vector3(.9f,.64f,.04f),gray);
        var trigger=new GameObject("Artifact display trigger"); trigger.transform.position=new Vector3(2.8f,1.23f,2.6f);
        var volume=trigger.AddComponent<BoxCollider>(); volume.size=new Vector3(.70f,.58f,.70f); volume.isTrigger=true;
        var snap=Marker(trigger.transform,"Snap Point",new Vector3(0,-.08f,0));
        var display=trigger.AddComponent<ArtifactDisplayTrigger>(); display.Configure("interaction-jar-01",ArtifactType.CanopicJar,snap,.35f,true,.23f); Set(display,"artifactLayers",1<<artifactLayer);

        var meleeTarget=Target("Melee compound target",new Vector3(-2.7f,1.3f,2.0f));
        var rangeTarget=Target("Range compound target",new Vector3(0,1.6f,6.5f));
        Box("Occlusion test wall",new Vector3(-4.3f,1.1f,4.3f),new Vector3(1.2f,2.2f,.20f),gray);
        Target("Hidden target behind wall",new Vector3(-4.3f,1.3f,5.1f));
        Box("Flashlight inspection wall",new Vector3(4.8f,1.5f,5.0f),new Vector3(1.6f,3,.12f),white);
        Text("Title", "VR INTERACTION LAB",new Vector3(0,3.25f,8.85f),.13f);
        Text("Bench labels","MELEE       LIGHT       RAY       BOW",new Vector3(0,1.0f,.25f),.035f);
        Text("Melee sign","01 / SWING\nMulti-collider target",new Vector3(-2.7f,2.35f,2f),.055f);
        Text("Range sign","02 / AIM + DRAW\nTrigger: ray | Release string: arrow",new Vector3(0,2.75f,6.4f),.055f);
        Text("Display sign","03 / RESTORE\nPlace jar on tray and release",new Vector3(2.8f,2f,2.6f),.045f);
        Text("Occlusion sign","SOLID WALL\nTarget behind",new Vector3(-4.3f,2.6f,4.15f),.045f);
        Text("Light sign","04 / FLASHLIGHT\nAim at this panel",new Vector3(4.8f,3.25f,4.9f),.045f);
        Box("Status board",new Vector3(-2.8f,2.9f,6.6f),new Vector3(3.7f,1.65f,.08f),dark);
        var status=Text("Live measurements","INTERACTION TEST / READY",new Vector3(-4.5f,3.58f,6.54f),.045f); status.anchor=TextAnchor.UpperLeft; status.alignment=TextAlignment.Left;
        var audio=services.AddComponent<AudioSource>(); audio.playOnAwake=false; audio.spatialBlend=0; audio.volume=.5f;
        var feedback=services.AddComponent<InteractionTestFeedback>();
        Set(feedback,"melee",melee); Set(feedback,"ray",ray); Set(feedback,"bow",bow); Set(feedback,"display",display); Set(feedback,"flashlight",flashlight); Set(feedback,"statusText",status); Set(feedback,"audioSource",audio);
        SetArray(feedback,"targetRenderers",meleeTarget.GetComponentsInChildren<Renderer>().Concat(rangeTarget.GetComponentsInChildren<Renderer>()).ToArray());
        var desktop=services.AddComponent<InteractionDesktopMode>(); Set(desktop,"xrRig",rig); Set(desktop,"xrCamera",cam); Set(desktop,"bow",bow); Set(desktop,"melee",melee);
        Text("Controls","VR: grip = grab / release   trigger = use\nDesktop: WASD move | RMB look | Tab hand\nQ grab / release | E use | arrows + R/F move hand",new Vector3(0,2.75f,3.9f),.042f);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.SaveAssets();
        SceneView.lastActiveSceneView?.LookAt(new Vector3(0,1.2f,2.8f),Quaternion.Euler(22,0,0),8);
        return "Created " + ScenePath + "; target/prop/artifact layers="+targetLayer+"/"+propLayer+"/"+artifactLayer;
    }

    static int Layer(string name)
    {
        int existing=LayerMask.NameToLayer(name); if(existing>=0)return existing;
        var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]); var layers=settings.FindProperty("layers");
        for(int i=8;i<32;i++) if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)){layers.GetArrayElementAtIndex(i).stringValue=name;settings.ApplyModifiedPropertiesWithoutUndo();return i;}
        throw new InvalidOperationException("No free layer");
    }
    static Material Mat(string name, Color color, bool unlit=false)
    {
        string path=Art+"/Materials/"+name+".mat"; var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat){mat=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
        mat.SetColor("_BaseColor",color); return mat;
    }
    static GameObject Box(string name,Vector3 pos,Vector3 scale,Material mat,Transform parent=null,bool collision=true)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent?parent:root,false);go.transform.localPosition=pos;go.transform.localScale=scale;
        go.GetComponent<Renderer>().sharedMaterial=mat;if(!collision)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());if(parent)go.layer=parent.gameObject.layer;return go;
    }
    static Transform Marker(Transform parent,string name,Vector3 position){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
    static GameObject Item(string name,Vector3 position,float mass)
    {
        var go=new GameObject(name);go.layer=propLayer;go.transform.position=position;var physical=go.AddComponent<PhysicalGrabbable>();Set(physical,"massKg",mass);Set(physical,"gripPose",Marker(go.transform,"Grip Pose",Vector3.zero));return go;
    }
    static void FinishItem(GameObject go)
    {
        var grab=go.GetComponent<XRGrabInteractable>();grab.colliders.Clear();grab.colliders.AddRange(go.GetComponentsInChildren<Collider>().Where(c=>!c.isTrigger));go.GetComponent<PhysicalGrabbable>().ApplyPhysicsSettings();
    }
    static LineRenderer Line(Transform parent,string name,float width)
    {
        var l=Marker(parent,name,Vector3.zero).gameObject.AddComponent<LineRenderer>();l.sharedMaterial=lineMat;l.startWidth=width;l.endWidth=width;l.positionCount=0;l.useWorldSpace=true;return l;
    }
    static GameObject Target(string name,Vector3 pos)
    {
        var go=new GameObject(name);go.transform.position=pos;go.layer=targetLayer;go.AddComponent<MeleeHitTarget>();
        Box("Target outer",Vector3.zero,new Vector3(.85f,.85f,.12f),blue,go.transform);
        Box("Target center",new Vector3(0,0,-.09f),new Vector3(.32f,.32f,.08f),gold,go.transform);
        Box("Target post",new Vector3(pos.x,pos.y*.5f,pos.z+.10f),new Vector3(.10f,pos.y,.10f),dark);return go;
    }
    static TextMesh Text(string name,string value,Vector3 pos,float size)
    {
        var go=new GameObject(name);go.transform.position=pos;go.transform.localScale=Vector3.one*(name=="Live measurements"?.42f:.25f);var t=go.AddComponent<TextMesh>();t.text=value;t.fontSize=64;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=Color.white;return t;
    }
    public static void Set(UnityEngine.Object obj,string field,object value)
    {
        var so=new SerializedObject(obj);var p=so.FindProperty(field);if(p==null)throw new Exception(obj.name+" missing field "+field);
        if(value is UnityEngine.Object u)p.objectReferenceValue=u;else if(value is int i)p.intValue=i;else if(value is float f)p.floatValue=f;else if(value is bool b)p.boolValue=b;else throw new Exception("Unsupported field "+field);
        so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(obj);
    }
    static void SetArray(UnityEngine.Object obj,string field,UnityEngine.Object[] values){var so=new SerializedObject(obj);var p=so.FindProperty(field);p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();}
}
