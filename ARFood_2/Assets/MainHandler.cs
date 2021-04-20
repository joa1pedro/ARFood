using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainHandler : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
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
                byte[] sendImage = ImageConversion.EncodeToJPG(texture, 70);
                NativeGallery.SaveImageToGallery(sendImage, "ARFood", "ArFood.jpg");
                //StartCoroutine(PostRequest(sendImage));
            }
        }, 1024);

        Debug.Log("Permission result: " + permission);
    }
}
