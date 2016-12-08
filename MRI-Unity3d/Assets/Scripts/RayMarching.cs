using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class RayMarching : MonoBehaviour
{
	[SerializeField]
	[Header("Render in a lower resolution to increase performance.")]
	private int downscale = 2;
	[SerializeField]
	private LayerMask volumeLayer;

	[SerializeField]
	private Shader compositeShader;
	[SerializeField]
	private Shader renderFrontDepthShader;
	[SerializeField]
	private Shader renderBackDepthShader;
	[SerializeField]
	private Shader rayMarchShader;

	[SerializeField][Header("Remove all the darker colors")]
	private bool increaseVisiblity = false;


	[Header("Drag all the textures in here")]
	[SerializeField]
	private Texture2D[] slices;
	[SerializeField][Range(0, 2)]
	private float opacity = 1;
	[Header("Volume texture size. These must be a power of 2")]
	[SerializeField]
	private int volumeWidth = 256;
	[SerializeField]
	private int volumeHeight = 256;
	[SerializeField]
	private int volumeDepth = 256;
	[Header("Clipping planes percentage")]
	[SerializeField]
	private Vector4 clipDimensions = new Vector4(100, 100, 100, 0);
    public void setClipDimension(float x)
    {
        clipDimensions = new Vector4(x, clipDimensions.y, clipDimensions.z, clipDimensions.w);
    }

	private Material _rayMarchMaterial;
	private Material _compositeMaterial;
	private Camera _ppCamera;
	private Texture3D _volumeBuffer;

    public bool create;
	private void Awake()
	{
		_rayMarchMaterial = new Material(rayMarchShader);

	}

	private void Start()
	{
		GenerateVolumeTexture();
	}

	private void OnDestroy()
	{
		if(_volumeBuffer != null)
		{
			Destroy(_volumeBuffer);
		}
	}


    [SerializeField]
	private Transform transverseClipPlane;//�����
    
    [SerializeField]
    private Transform sagittalClipPlane;//ʸ״��

    [SerializeField]
    private Transform coronalClipPlane;//��״��

    [SerializeField]
	private Transform cubeTarget;

    void OnValidate()// restrict some value here
    {
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		_rayMarchMaterial.SetTexture("_VolumeTex", _volumeBuffer);

		var width = source.width / downscale;
		var height = source.height / downscale;

		if(_ppCamera == null)
		{
			var go = new GameObject("PPCamera");
			_ppCamera = go.AddComponent<Camera>();
			_ppCamera.enabled = false;
		}

		_ppCamera.CopyFrom(GetComponent<Camera>());
		_ppCamera.clearFlags = CameraClearFlags.SolidColor;
		_ppCamera.backgroundColor = Color.white;
		_ppCamera.cullingMask = volumeLayer;

		var frontDepth = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGBFloat);
		var backDepth = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGBFloat);
        var volumeTarget = RenderTexture.GetTemporary(width, height, 0);

		// need to set this vector because unity bakes object that are non uniformily scaled
		//TODO:FIX
		//Shader.SetGlobalVector("_VolumeScale", cubeTarget.transform.localScale);

		// Render depths
		_ppCamera.targetTexture = frontDepth;
		_ppCamera.RenderWithShader(renderFrontDepthShader, "RenderType");
		_ppCamera.targetTexture = backDepth;
		_ppCamera.RenderWithShader(renderBackDepthShader, "RenderType");

		// Render volume
		_rayMarchMaterial.SetTexture("_FrontTex", frontDepth);
		_rayMarchMaterial.SetTexture("_BackTex", backDepth);

		if(cubeTarget != null && transverseClipPlane != null && transverseClipPlane.gameObject.activeSelf)
		{
			var p = new Plane(
				cubeTarget.InverseTransformDirection(transverseClipPlane.transform.up), 
				cubeTarget.InverseTransformPoint(transverseClipPlane.position));
			_rayMarchMaterial.SetVector("_TransverseClipPlane", new Vector4(p.normal.x, p.normal.y, p.normal.z, p.distance));
		}
		else
		{
			_rayMarchMaterial.SetVector("_TransverseClipPlane", Vector4.zero);
		}

        if (cubeTarget != null && sagittalClipPlane != null && sagittalClipPlane.gameObject.activeSelf)
        {
            var p = new Plane(
                cubeTarget.InverseTransformDirection(sagittalClipPlane.transform.up),
                cubeTarget.InverseTransformPoint(sagittalClipPlane.position));
            _rayMarchMaterial.SetVector("_SagittalClipPlane", new Vector4(p.normal.x, p.normal.y, p.normal.z, p.distance));
        }
        else
        {
            _rayMarchMaterial.SetVector("_SagittalClipPlane", Vector4.zero);
        }


        if (cubeTarget != null && coronalClipPlane != null && coronalClipPlane.gameObject.activeSelf)
        {
            var p = new Plane(
                cubeTarget.InverseTransformDirection(coronalClipPlane.transform.up),
                cubeTarget.InverseTransformPoint(coronalClipPlane.position));
            _rayMarchMaterial.SetVector("_CoronalClipPlane", new Vector4(p.normal.x, p.normal.y, p.normal.z, p.distance));
        }
        else
        {
            _rayMarchMaterial.SetVector("_CoronalClipPlane", Vector4.zero);
        }

        _rayMarchMaterial.SetFloat("_Opacity", opacity); // Blending strength 
		_rayMarchMaterial.SetVector("_ClipDims", clipDimensions / 100f); // Clip box




        Graphics.Blit(volumeTarget, null, _rayMarchMaterial);


        if (create)
        {
           
            Texture2D picture = new Texture2D(256, 256, TextureFormat.RGB24, false);
            Rect rect = new Rect(0, 0, Screen.width, Screen.height);
            picture.ReadPixels(rect, 0, 0);
            picture.Apply();

            // �����Щ�������ݣ���һ��pngͼƬ�ļ�
            byte[] bytes = picture.EncodeToPNG();
            string filename = Application.dataPath + "/Screenshot.png";
            System.IO.File.WriteAllBytes(filename, bytes);
            Debug.Log(string.Format("save a screenshot: {0}", filename));
        }

        RenderTexture.ReleaseTemporary(volumeTarget);
		RenderTexture.ReleaseTemporary(frontDepth);
		RenderTexture.ReleaseTemporary(backDepth);
	}

	private void GenerateVolumeTexture()
	{
		// sort
		System.Array.Sort(slices, (x, y) => x.name.CompareTo(y.name));
		
		// use a bunch of memory!
		_volumeBuffer = new Texture3D(volumeWidth, volumeHeight, volumeDepth, TextureFormat.ARGB32, false);
		
		var w = _volumeBuffer.width;
		var h = _volumeBuffer.height;
		var d = _volumeBuffer.depth;
		
		// skip some slices if we can't fit it all in
		var countOffset = (slices.Length - 1) / (float)d;
		
		var volumeColors = new Color[w * h * d];
		
		var sliceCount = 0;
		var sliceCountFloat = 0f;
		for(int z = 0; z < d; z++)
		{
			sliceCountFloat += countOffset;
			sliceCount = Mathf.FloorToInt(sliceCountFloat);
			for(int x = 0; x < w; x++)
			{
				for(int y = 0; y < h; y++)
				{
					var idx = x + (y * w) + (z * (w * h));
					volumeColors[idx] = slices[sliceCount].GetPixelBilinear(x / (float)w, y / (float)h); 
					if(increaseVisiblity)
					{
						volumeColors[idx].a *= volumeColors[idx].r;
					}
				}
			}
		}
		
		_volumeBuffer.SetPixels(volumeColors);
		_volumeBuffer.Apply();
		
		_rayMarchMaterial.SetTexture("_VolumeTex", _volumeBuffer);
	}
}
