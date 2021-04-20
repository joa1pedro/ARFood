/*===============================================================================
Copyright (c) 2016-2018 PTC Inc. All Rights Reserved.

Vuforia is a trademark of PTC Inc., registered in the United States and other
countries.
===============================================================================*/
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.UI;
using Vuforia;
using UnityEngine.Networking;
using Firebase;
using Firebase.Database;
/// <summary>
/// A custom handler which uses the VuMarkManager.
/// </summary>
public class VuMarkHandler : MonoBehaviour
{
    #region PRIVATE_MEMBER_VARIABLES
    // Define the number of persistent child objects of the VuMarkBehaviour. When
    // destroying the instance-specific augmentations, it will start after this value.
    // Persistent Children:
    // 1. Canvas -- displays info about the VuMark
    // 2. LineRenderer -- displays border outline around VuMark
    const int PersistentNumberOfChildren = 2;
    VuMarkManager vumarkManager;
    LineRenderer lineRenderer;
    Dictionary<string, Texture2D> vumarkInstanceTextures;
    Dictionary<string, GameObject> vumarkAugmentationObjects;

    DatabaseReference mDatabaseReference;
    PanelShowHide nearestVuMarkScreenPanel;
    VuMarkTarget closestVuMark;
    VuMarkTarget currentVuMark;
    Camera vuforiaCamera;
    byte[] image;
    DatabaseReference dbreference;
    string prefix = "one unit";

    #endregion // PRIVATE_MEMBER_VARIABLES

    #region PUBLIC_MEMBERS
    public GameObject cardText;
    public string nutritionTest;
    public string status = "ok";
    public string vuMarkId = "Take a Picture"; //Food Family
    public string vuMarkDesc = ""; //Perc
    public string vuMarkDataType = "Take a Picture"; //Foor reco


    [System.Serializable]
    public class AugmentationObject
    {
        public string vumarkID;
        public GameObject augmentation;
    }
    public AugmentationObject[] augmentationObjects;
    #endregion // PUBLIC_MEMBERS

    #region SYSTEM_CLASSES
    [System.Serializable]
    public class NutritionData
    {
        public float calories;
        public float totalWeight;
        public totalNutrients totalNutrients;
    }
    [System.Serializable]
    public class ENERC_KCAL
    {
        public string label;
        public float quantity;
        public string unit;
    }
    [System.Serializable]
    public class FAT
    {
        public string label;
        public float quantity;
        public string unit;
    }
    [System.Serializable]
    public class FASAT
    {
        public string label;
        public float quantity;
        public string unit;
    }
    [System.Serializable]
    public class FATRN
    {
        public string label;
        public float quantity;
        public string unit;
    }
    [System.Serializable]
    public class CHOCDF
    {
        public string label;
        public float quantity;
        public string unit;
    }
    [System.Serializable]
    public class SUGAR
    {
        public string label;
        public float quantity;
        public string unit;
    }
    [System.Serializable]
    public class PROCNT
    {
        public string label;
        public float quantity;
        public string unit;
    }
    [System.Serializable]
    public class totalNutrients
    {
        public ENERC_KCAL ENERC_KCAL;
        public FAT FAT;
        public FASAT FASAT;
        public FATRN FATRN;
        public CHOCDF CHOCDF;
        public SUGAR SUGAR;
        public PROCNT PROCNT;
    }

    ////////////////////////////////////////////////////////////////////////
    [System.Serializable]
    public class Recognition
    {
        public int id;
        public string name;
        public float prob;
        public string subclasses;
    }
    [System.Serializable]
    public static class JsonHelper
    {
        public static T[] FromJson<T>(string json, int id)
        {
            if (id == 1)
            {
                Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(json);
                return wrapper.recognition_results;
            }
            else if (id == 2)
            {
                Wrapper2<T> wrapper = JsonUtility.FromJson<Wrapper2<T>>(json);
                return wrapper.foodFamily;
            }
            else
            {
                Wrapper2<T> wrapper = JsonUtility.FromJson<Wrapper2<T>>(json);
                return wrapper.foodFamily;
            }

        }
        [System.Serializable]
        private class Wrapper<T>
        {
            public T[] recognition_results;
        }
        private class Wrapper2<T>
        {
            public T[] foodFamily;
        }
    }
    #endregion // SYSTEM_CLASSES
    #region MONOBEHAVIOUR_METHODS
    void Awake()
    {
        VuforiaConfiguration.Instance.Vuforia.MaxSimultaneousImageTargets = 10; // Set to 10 for VuMarks
    }

    void Start()
    {
        VuforiaARController.Instance.RegisterVuforiaStartedCallback(OnVuforiaStarted);

        this.vumarkInstanceTextures = new Dictionary<string, Texture2D>();
        this.vumarkAugmentationObjects = new Dictionary<string, GameObject>();

        foreach (AugmentationObject obj in this.augmentationObjects)
        {
            this.vumarkAugmentationObjects.Add(obj.vumarkID, obj.augmentation);
        }

        // Hide the initial VuMark Template when the scene starts.
        VuMarkBehaviour vumarkBehaviour = FindObjectOfType<VuMarkBehaviour>();
        if (vumarkBehaviour)
        {
            ToggleRenderers(vumarkBehaviour.gameObject, false);
        }
        cardText.GetComponent<TextMesh>().text = "HELLO";
        this.nearestVuMarkScreenPanel = FindObjectOfType<PanelShowHide>();

        status = "busy";
        Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
            var dependencyStatus = task.Result;
            if (dependencyStatus == Firebase.DependencyStatus.Available)
            {
                // Create and hold a reference to your FirebaseApp,
                // where app is a Firebase.FirebaseApp property of your application class.
                var app = Firebase.FirebaseApp.DefaultInstance;
                status = "ok";
                // Set a flag here to indicate whether Firebase is ready to use by your app.
            }
            else
            {
                UnityEngine.Debug.LogError(System.String.Format(
                  "Could not resolve all Firebase dependencies: {0}", dependencyStatus));
                // Firebase Unity SDK is not safe to use here.]
                Debug.Log("Firebase couldn't resolve, solve this before continuing or disable firebase things");
                status = "ok";
            }
        });
        if (status == "ok") {
            dbreference = FirebaseDatabase.DefaultInstance.RootReference;

        }
        //StartCoroutine(GetNutritionalData(nutritionTest));
    }



    void Update()
    {
        if (status == "ok")
        {
            UpdateClosestTarget();
        }

    }

    void OnDestroy()
    {
        VuforiaConfiguration.Instance.Vuforia.MaxSimultaneousImageTargets = 4; // Reset back to 4 when exiting
        // Unregister callbacks from VuMark Manager
        this.vumarkManager.UnregisterVuMarkBehaviourDetectedCallback(OnVuMarkBehaviourDetected);
        this.vumarkManager.UnregisterVuMarkDetectedCallback(OnVuMarkDetected);
        this.vumarkManager.UnregisterVuMarkLostCallback(OnVuMarkLost);
    }

    #endregion // MONOBEHAVIOUR_METHODS

    void OnVuforiaStarted()
    {
        // register callbacks to VuMark Manager
        this.vumarkManager = TrackerManager.Instance.GetStateManager().GetVuMarkManager();
        this.vumarkManager.RegisterVuMarkBehaviourDetectedCallback(OnVuMarkBehaviourDetected);
        this.vumarkManager.RegisterVuMarkDetectedCallback(OnVuMarkDetected);
        this.vumarkManager.RegisterVuMarkLostCallback(OnVuMarkLost);
        this.vuforiaCamera = VuforiaBehaviour.Instance.GetComponent<Camera>();
    }

    #region VUMARK_CALLBACK_METHODS

    /// <summary>
    ///  Register a callback which is invoked whenever a VuMark-result is newly detected which was not tracked in the frame before
    /// </summary>
    /// <param name="vumarkBehaviour"></param>
    public void OnVuMarkBehaviourDetected(VuMarkBehaviour vumarkBehaviour)
    {
        Debug.Log("<color=cyan>VuMarkHandler.OnVuMarkBehaviourDetected(): </color>" + vumarkBehaviour.TrackableName);

        GenerateVuMarkBorderOutline(vumarkBehaviour);

        ToggleRenderers(vumarkBehaviour.gameObject, true);

        // Check for existance of previous augmentations and delete before instantiating new ones.
        DestroyChildAugmentationsOfTransform(vumarkBehaviour.transform);

        StartCoroutine(OnVuMarkTargetAvailable(vumarkBehaviour));

    }

    IEnumerator OnVuMarkTargetAvailable(VuMarkBehaviour vumarkBehaviour)
    {

        // We need to wait until VuMarkTarget is available
        yield return new WaitUntil(() => vumarkBehaviour.VuMarkTarget != null);

        Debug.Log("<color=green>VuMarkHandler.OnVuMarkTargetAvailable() called: </color>" + GetVuMarkId(vumarkBehaviour.VuMarkTarget));

        SetVuMarkInfoForCanvas(vumarkBehaviour);
        SetVuMarkAugmentation(vumarkBehaviour);
        SetVuMarkOpticalSeeThroughConfig(vumarkBehaviour);

    }

    /// <summary>
    /// This method will be called whenever a new VuMark is detected
    /// </summary>
    public void OnVuMarkDetected(VuMarkTarget vumarkTarget)
    {

        Debug.Log("<color=cyan>VuMarkHandler.OnVuMarkDetected(): </color>" + GetVuMarkId(vumarkTarget));

        // Check if this VuMark's ID already has a stored texture. Generate and store one if not.
        if (RetrieveStoredTextureForVuMarkTarget(vumarkTarget) == null)
        {
            this.vumarkInstanceTextures.Add(GetVuMarkId(vumarkTarget), GenerateTextureFromVuMarkInstanceImage(vumarkTarget));
        }

    }

    /// <summary>
    /// This method will be called whenever a tracked VuMark is lost
    /// </summary>
    public void OnVuMarkLost(VuMarkTarget vumarkTarget)
    {

        Debug.Log("<color=cyan>VuMarkHandler.OnVuMarkLost(): </color>" + GetVuMarkId(vumarkTarget));

        if (vumarkTarget == this.currentVuMark)
            this.nearestVuMarkScreenPanel.ResetShowTrigger();
    }

    #endregion // VUMARK_CALLBACK_METHODS


    #region PRIVATE_METHODS

    string GetVuMarkDataType(VuMarkTarget vumarkTarget)
    {
        switch (vumarkTarget.InstanceId.DataType)
        {
            case InstanceIdType.BYTES:
                return "Bytes";
            case InstanceIdType.STRING:
                return "String";
            case InstanceIdType.NUMERIC:
                return "Numeric";
        }
        return string.Empty;
    }

    string GetVuMarkId(VuMarkTarget vumarkTarget)
    {
        switch (vumarkTarget.InstanceId.DataType)
        {
            case InstanceIdType.BYTES:
                return vumarkTarget.InstanceId.HexStringValue;
            case InstanceIdType.STRING:
                return vumarkTarget.InstanceId.StringValue;
            case InstanceIdType.NUMERIC:
                return vumarkTarget.InstanceId.NumericValue.ToString();
        }
        return string.Empty;
    }

    Sprite GetVuMarkImage(VuMarkTarget vumarkTarget)
    {
        var instanceImage = vumarkTarget.InstanceImage;
        if (instanceImage == null)
        {
            Debug.Log("VuMark Instance Image is null.");
            return null;
        }

        // First we create a texture
        Texture2D texture = new Texture2D(instanceImage.Width, instanceImage.Height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp
        };
        instanceImage.CopyToTexture(texture);

        // Then we turn the texture into a Sprite
        Debug.Log("<color=cyan>Image: </color>" + instanceImage.Width + "x" + instanceImage.Height);
        if (texture.width == 0 || texture.height == 0)
            return null;
        Rect rect = new Rect(0, 0, texture.width, texture.height);
        return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f));
    }

    string GetNumericVuMarkDescription(VuMarkTarget vumarkTarget)
    {
        int vumarkIdNumeric;

        if (int.TryParse(GetVuMarkId(vumarkTarget), NumberStyles.Integer, CultureInfo.InvariantCulture, out vumarkIdNumeric))
        {
            // Change the description based on the VuMark ID
            switch (vumarkIdNumeric % 4)
            {
                case 1:
                    return "Astronaut";
                case 2:
                    return "Drone";
                case 3:
                    return "Fissure";
                case 0:
                    return "Oxygen Tank";
                default:
                    return "Unknown";
            }
        }

        return string.Empty; // if VuMark DataType is byte or string
    }

    void SetVuMarkInfoForCanvas(VuMarkBehaviour vumarkBehaviour)
    {
        Text canvasText = vumarkBehaviour.gameObject.GetComponentInChildren<Text>();
        UnityEngine.UI.Image canvasImage = vumarkBehaviour.gameObject.GetComponentsInChildren<UnityEngine.UI.Image>()[2];

        Texture2D vumarkInstanceTexture = RetrieveStoredTextureForVuMarkTarget(vumarkBehaviour.VuMarkTarget);
        Rect rect = new Rect(0, 0, vumarkInstanceTexture.width, vumarkInstanceTexture.height);
        /*
        string vuMarkId = GetVuMarkId(vumarkBehaviour.VuMarkTarget);
        string vuMarkDesc = GetVuMarkDataType(vumarkBehaviour.VuMarkTarget);
        string vuMarkDataType = GetNumericVuMarkDescription(vumarkBehaviour.VuMarkTarget);
         */
        //string vuMarkId = "ArFood"; //Food Family
        //string vuMarkDesc = "Arfood"; //Perc
        //string vuMarkDataType = "ArFood"; //Foor reco

        canvasText.text =
            "<color=yellow>Food Family: </color>" +
            "\n" + vuMarkDesc +
            "\n\n<color=yellow>Food Recognition: </color>" +
            "\n" + vuMarkDataType;

        if (vumarkInstanceTexture.width == 0 || vumarkInstanceTexture.height == 0)
            canvasImage.sprite = null;
        else
            canvasImage.sprite = Sprite.Create(vumarkInstanceTexture, rect, new Vector2(0.5f, 0.5f));
    }

    void SetVuMarkAugmentation(VuMarkBehaviour vumarkBehaviour)
    {
        GameObject sourceAugmentation = GetValueFromDictionary(this.vumarkAugmentationObjects, GetVuMarkId(vumarkBehaviour.VuMarkTarget));

        if (sourceAugmentation)
        {
            GameObject augmentation = Instantiate(sourceAugmentation);
            augmentation.transform.SetParent(vumarkBehaviour.transform);
            augmentation.transform.localPosition = Vector3.zero;
            augmentation.transform.localEulerAngles = Vector3.zero;
        }
    }

    void SetVuMarkOpticalSeeThroughConfig(VuMarkBehaviour vumarkBehaviour)
    {
        // Check to see if we're running on a HoloLens device.
        if (IsHolographicDevice())
        {
            MeshRenderer meshRenderer = vumarkBehaviour.GetComponent<MeshRenderer>();

            // If the VuMark has per instance background info, turn off virtual target so that it doesn't cover modified physical target
            if (vumarkBehaviour.VuMarkTemplate.TrackingFromRuntimeAppearance)
            {
                if (meshRenderer)
                {
                    meshRenderer.enabled = false;
                }
            }
            else
            {
                // If the VuMark background is part of VuMark Template and same per instance, render the virtual target
                if (meshRenderer)
                {
                    meshRenderer.material.mainTexture = RetrieveStoredTextureForVuMarkTarget(vumarkBehaviour.VuMarkTarget);
                }
            }
        }
        else
        {
            MeshRenderer meshRenderer = vumarkBehaviour.GetComponent<MeshRenderer>();

            if (meshRenderer)
            {
                meshRenderer.enabled = false;
            }
        }
    }

    bool IsHolographicDevice()
    {
#if UNITY_2019_3_OR_NEWER
        var xrDisplaySubsystems = new List<XRDisplaySubsystem>();
        SubsystemManager.GetInstances(xrDisplaySubsystems);
        foreach (var xrDisplay in xrDisplaySubsystems)
        {
            if (xrDisplay.running && xrDisplay.displayOpaque)
            {
                return true;
            }
        }
        return false;
#else
        return XRDevice.isPresent && !UnityEngine.XR.WSA.HolographicSettings.IsDisplayOpaque;
#endif
    }

    Texture2D RetrieveStoredTextureForVuMarkTarget(VuMarkTarget vumarkTarget)
    {
        return GetValueFromDictionary(this.vumarkInstanceTextures, GetVuMarkId(vumarkTarget));
    }

    Texture2D GenerateTextureFromVuMarkInstanceImage(VuMarkTarget vumarkTarget)
    {
        Debug.Log("<color=cyan>SaveImageAsTexture() called.</color>");

        if (vumarkTarget.InstanceImage == null)
        {
            Debug.Log("VuMark Instance Image is null.");
            return null;
        }
        Debug.Log(vumarkTarget.InstanceImage.Width + "," + vumarkTarget.InstanceImage.Height);

        Texture2D texture = new Texture2D(vumarkTarget.InstanceImage.Width, vumarkTarget.InstanceImage.Height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp
        };

        vumarkTarget.InstanceImage.CopyToTexture(texture, false);

        return texture;
    }



    void GenerateVuMarkBorderOutline(VuMarkBehaviour vumarkBehaviour)
    {
        this.lineRenderer = vumarkBehaviour.GetComponentInChildren<LineRenderer>();

        if (this.lineRenderer == null)
        {
            Debug.Log("<color=green>Existing Line Renderer not found. Creating new one.</color>");
            GameObject vumarkBorder = new GameObject("VuMarkBorder");
            vumarkBorder.transform.SetParent(vumarkBehaviour.transform);
            vumarkBorder.transform.localPosition = Vector3.zero;
            vumarkBorder.transform.localEulerAngles = Vector3.zero;
            vumarkBorder.transform.localScale =
                new Vector3(
                    1 / vumarkBehaviour.transform.localScale.x,
                    1,
                    1 / vumarkBehaviour.transform.localScale.z);
            this.lineRenderer = vumarkBorder.AddComponent<LineRenderer>();
            this.lineRenderer.enabled = false;
            this.lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            this.lineRenderer.receiveShadows = false;
            // This shader needs to be added in the Project's Graphics Settings,
            // unless it is already in use by a Material present in the project.
            this.lineRenderer.material.shader = Shader.Find("Unlit/Color");
            this.lineRenderer.material.color = Color.clear;
            this.lineRenderer.positionCount = 4;
            this.lineRenderer.loop = true;
            this.lineRenderer.useWorldSpace = false;
            Vector2 vumarkSize = vumarkBehaviour.GetSize();
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 1.0f);
            curve.AddKey(1.0f, 1.0f);
            this.lineRenderer.widthCurve = curve;
            this.lineRenderer.widthMultiplier = 0.003f;
            float vumarkExtentsX = (vumarkSize.x * 0.5f) + (this.lineRenderer.widthMultiplier * 0.5f);
            float vumarkExtentsZ = (vumarkSize.y * 0.5f) + (this.lineRenderer.widthMultiplier * 0.5f);
            this.lineRenderer.SetPositions(new Vector3[]
            {
                new Vector3(-vumarkExtentsX, 0.001f, vumarkExtentsZ),
                new Vector3(vumarkExtentsX, 0.001f, vumarkExtentsZ),
                new Vector3(vumarkExtentsX, 0.001f, -vumarkExtentsZ),
                new Vector3(-vumarkExtentsX, 0.001f, -vumarkExtentsZ)
            });
        }
    }

    void DestroyChildAugmentationsOfTransform(Transform parent)
    {
        if (parent.childCount > PersistentNumberOfChildren)
        {
            for (int x = PersistentNumberOfChildren; x < parent.childCount; x++)
            {
                Destroy(parent.GetChild(x).gameObject);
            }
        }
    }

    T GetValueFromDictionary<T>(Dictionary<string, T> dictionary, string key)
    {
        if (dictionary.ContainsKey(key))
        {
            T value;
            dictionary.TryGetValue(key, out value);
            return value;
        }
        return default(T);
    }

    void ToggleRenderers(GameObject obj, bool enable)
    {
        var rendererComponents = obj.GetComponentsInChildren<Renderer>(true);
        var canvasComponents = obj.GetComponentsInChildren<Canvas>(true);

        foreach (var component in rendererComponents)
        {
            // Skip the LineRenderer
            if (!(component is LineRenderer))
            {
                component.enabled = enable;
            }
        }

        foreach (var component in canvasComponents)
        {
            component.enabled = enable;
        }
    }

    void UpdateClosestTarget()
    {
        if (VuforiaRuntimeUtilities.IsVuforiaEnabled() && VuforiaARController.Instance.HasStarted)
        {
            float closestDistance = Mathf.Infinity;

            foreach (VuMarkBehaviour vumarkBehaviour in this.vumarkManager.GetActiveBehaviours())
            {
                Vector3 worldPosition = vumarkBehaviour.transform.position;
                Vector3 camPosition = this.vuforiaCamera.transform.InverseTransformPoint(worldPosition);

                float distance = Vector3.Distance(Vector2.zero, camPosition);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    this.closestVuMark = vumarkBehaviour.VuMarkTarget;
                }
            }

            if (this.closestVuMark != null &&
                this.currentVuMark != this.closestVuMark)
            {

                /*
                var vuMarkId = GetVuMarkId(this.closestVuMark);
                var vuMarkDataType = GetVuMarkDataType(this.closestVuMark);
                var vuMarkImage = GetVuMarkImage(this.closestVuMark);
                var vuMarkDesc = GetNumericVuMarkDescription(this.closestVuMark);
                */

                //var vuMarkId = "ARFood"; //Food Family
                //var vuMarkDataType = "ARFood"; //Food recog + perc
                var vuMarkImage = GetVuMarkImage(this.closestVuMark);
                //var vuMarkDesc = "Descrição"; // perc



                this.currentVuMark = this.closestVuMark;

                StartCoroutine(ShowPanelAfter(0.5f, vuMarkDataType, vuMarkDesc, vuMarkImage));
            }
        }
    }

    IEnumerator ShowPanelAfter(float seconds, string vuMarkDataType, string vuMarkDesc, Sprite vuMarkImage)
    {
        yield return new WaitForSeconds(seconds);

        if (this.nearestVuMarkScreenPanel)
        {
            nearestVuMarkScreenPanel.Show(vuMarkDataType, vuMarkDesc, vuMarkImage);
        }
    }

    public void TakePicture()
    {
        NativeCamera.Permission permission = NativeCamera.TakePicture((path) =>
        {
            Debug.Log("Image path: " + path);
            if (path != null)
            {
                // Create a Texture2D from the captured image
                Texture2D texture = NativeCamera.LoadImageAtPath(path, 1024, false);
                if (texture == null)
                {
                    Debug.Log("Couldn't load texture from " + path);
                    return;
                }
                texture.Apply();
                byte[] sendImage = ImageConversion.EncodeToJPG(texture, 40);
                NativeGallery.SaveImageToGallery(sendImage, "ARFood", "ArFood.jpg");
                StartCoroutine(PostRequest(sendImage));
            }
        }, 1024);

        Debug.Log("Permission result: " + permission);
    }
    public void TestJson()
    {
        string fromLogMeal = "{\n   \"foodFamily\": [" +
           "\n        {" +
           "\n             \"id\": 7," +
           "\n             \"name\": \"noodles/pasta\"," +
           "\n             \"prob\": 0.9964700222015381" +
           "\n        }" +
           "\n    ]," +
           "\n    \"recognition_results\": [" +
           "\n        {" +
           "\n            \"id\": 241," +
           "\n            \"name\": \"spaghetti bolognese\"," +
           "\n            \"prob\": 0.6410658177127687," +
           "\n            \"subclasses\": []" +
           "\n        }," +
           "\n        {" +
           "\n            \"id\": 1236," +
           "\n            \"name\": \"carrot\"," +
           "\n            \"prob\": 0.13987858597921526," +
           "\n            \"subclasses\": []" +
           "\n        }," +
           "\n        {" +
           "\n            \"id\": 2128," +
           "\n            \"name\": \"green soybean sprout\"," +
           "\n            \"prob\": 0.06023694223602742," +
           "\n            \"subclasses\": []" +
           "\n        }," +
           "\n        {" +
           "\n            \"id\": 1031," +
           "\n            \"name\": \"pasta with bolognese\"," +
           "\n            \"prob\": 0.01628351200353942," +
           "\n            \"subclasses\": [" +
           "\n                {" +
           "\n                    \"id\": 241," +
           "\n                    \"name\": \"spaghetti bolognese\"," +
           "\n                    \"prob\": 0.9751489639282227}]" +
           "\n                }," +
           "\n        {" +
           "\n            \"id\": 1968," +
           "\n            \"name\": \"lobster prepared\"," +
           "\n            \"prob\": 0.005521060155690652," +
           "\n            \"subclasses\": []" +
           "\n        }," +
           "\n        {" +
           "\n            \"id\": 645," +
           "\n            \"name\": \"hot and dry noodle\"," +
           "\n            \"prob\": 2.2994437239130058e-05," +
           "\n            \"subclasses\": []" +
           "\n        }" +
           "\n    ]" +
           "\n}";

        string teste = "{" +
           "\n    \"id\": 7," +
           "\n    \"name\": \"noodles/pasta\"," +
           "\n    \"prob\": 0.9964700222015381" +
           "\n}";

        string teste2 = "{\n   \"foodFamily\": [" +
           "\n    {" +
           "\n    \"id\": 7," +
           "\n    \"name\": \"noodles/pasta\"," +
           "\n    \"prob\": 0.9964700222015381" +
           "\n   }" +
           "\n    ]" +
           "\n}";
        Debug.Log(fromLogMeal);
        Recognition[] Test = JsonHelper.FromJson<Recognition>(fromLogMeal, 1);
        Debug.Log("Recog Results Size" + Test.Length);
    }
    public void setData(string vuMarkDescAux, string vuMarkDataTypeAux, string vuMarkDataTypeAux2)
    {
        //vuMarkId = vuMarkIdAux; //Food Family
        vuMarkDesc = vuMarkDescAux; //Perc
        vuMarkDataType = vuMarkDataTypeAux + " - " + vuMarkDataTypeAux2; //Foor reco
    }


    public IEnumerator PostRequest(byte[] data)
    {
        status = "busy";
        string postURL = "api.logmeal.es/v2/recognition/dish";

        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
        MultipartFormFileSection myformFile = new MultipartFormFileSection("image", data, "image.jpg", "multipart/form-data");

        formData.Add(myformFile);

        //MultipartFormDataSection myformData1 = new MultipartFormDataSection("num_tag", "3");
        //MultipartFormDataSection myformData2 = new MultipartFormDataSection("api_key", "78468d38de512c206c5fb5678bbf7a85b897c3e7");
        //formData.Add(myformData1);
        //formData.Add(myformData2);


        UnityWebRequest www = UnityWebRequest.Post(postURL, formData);
        www.SetRequestHeader("Authorization", "Bearer f4fa168090cc068c22a54dae37a2fdf4ed0b4029");


        yield return www.SendWebRequest();


        if (www.isNetworkError || www.isHttpError)
        {
            Debug.LogError(www.error);
        }
        else
        {
            string Aux = "";
            Debug.Log(www.downloadHandler.text);
            Recognition[] foodFamily = JsonHelper.FromJson<Recognition>(www.downloadHandler.text, 2);
            Recognition[] recog = JsonHelper.FromJson<Recognition>(www.downloadHandler.text, 1);
            for (int i = 0; i < foodFamily.Length; i++)
            {
                if (i < foodFamily.Length - 1)
                {
                    Aux += foodFamily[i].name + " - " + foodFamily[i].prob.ToString() + "\n";
                }
                else
                {
                    Aux += foodFamily[i].name + " - " + foodFamily[i].prob.ToString();
                }

            }
            setData(Aux, FindBestResult(recog).name, FindBestResult(recog).prob.ToString());
            StartCoroutine(GetNutritionalData(FindBestResult(recog).name));
            status = "ok";
        }

    }

    public IEnumerator GetNutritionalData(string ingr)
    {
        string getURL = "api.edamam.com/api/nutrition-data";
        string app_id = "f9e88c67";
        string app_key = "c9561133d4c791a9a68dd8c47d4d5bc7";

        using (UnityWebRequest www = UnityWebRequest.Get(getURL + "?app_id=" + app_id + "&app_key=" + app_key + "&ingr=" + prefix + " of " + ingr))
        {
            // Request and wait for the desired page.
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Debug.Log(www.error);
            }
            else
            {
                // Show results as text
                //Debug.Log(www.downloadHandler.text);
                NutritionData nd = JsonUtility.FromJson<NutritionData>(www.downloadHandler.text);

                SetCardText(nd, ingr);
                SaveToDatabase(www.downloadHandler.text.Insert(1, "\"call\": \"" + prefix + ingr + "\","));
            }

        }
    }

    public void updatePrefix (string info)
    {
        prefix = info;
    
    }

    Recognition FindBestResult(Recognition[] recog)
    {
        int ind = 0;
        float aux = 0;
        for (int i=0; i<recog.Length; i++)
        {
           if (recog[i].prob > aux)
            {
                aux = recog[i].prob;
                ind = i;
            }
        }

        return recog[ind];
    }

    void SaveToDatabase(string json)
    {
        Debug.Log(json);
        dbreference.Child("user").Child(System.DateTime.Now.ToString("ddMMyyyy")).SetRawJsonValueAsync(json);
    }
    
    void SetCardText(NutritionData data, string name)
    {

        if(data.calories == 0)
        {
            cardText.GetComponent<TextMesh>().text = "Não encontrado.";
        }
        else
        {
            cardText.GetComponent<TextMesh>().text =
                "Alimento: " + name.ToUpper() +
                "\n Calorias: " + data.calories.ToString() +
                "\n Gorduras: " + data.totalWeight.ToString() +
                "\n Saturadas: " + data.totalNutrients.FASAT.quantity.ToString() + data.totalNutrients.FASAT.unit +
                "\n Carboidratos: " + data.totalNutrients.CHOCDF.quantity.ToString() + data.totalNutrients.CHOCDF.unit +
                "\n Proteinas: " + data.totalNutrients.PROCNT.quantity.ToString() + data.totalNutrients.PROCNT.unit +
                "\n Açúcares: " + data.totalNutrients.SUGAR.quantity.ToString() + data.totalNutrients.SUGAR.unit;
        }

    }

    #endregion // PRIVATE_METHODS
}
