using UnityEngine;
using System.Collections.Generic;

public class CubeGrid : MonoBehaviour
{
	public int gridSize = 10;
	public float gridScale = 0.3f;
	public float cubeScale = 0.06f;

	GameObject cubeParent;

	void Start ()
	{
		cubeParent = new GameObject ("Cubes");
		cubeParent.transform.parent = transform;
		cubeParent.SetActive (false);

		int S = gridSize / 2;
		int S2 = gridSize / 4;

		System.Func<Color, Color> alternate = (Color c) => {
			float h, s, v;
			Color.RGBToHSV (c, out h, out s, out v);
			return Color.HSVToRGB (h, s * 0.25f, v * 0.75f);
		};

		for (int i = -S; i <= S; i++)
			for (int j = -S; j <= S; j++)
				for (int k = -S; k <= S; k++) {
					if (i == 0 && j == 0 && k == 0)
						continue;
					
					var go = GameObject.CreatePrimitive (PrimitiveType.Cube);
					var r = go.GetComponent<Renderer> ();
					var t = go.transform;

					SetMaterial (r, i % S2 == 0 || j % S2 == 0 || k % S2 == 0 ? alternate (Color.white) : Color.white);
					if (j == 0)
						SetMaterial (r, i % S2 == 0 || k % S2 == 0 ? alternate (Color.red) : Color.red);
					if (i == 0)
						SetMaterial (r, j % S2 == 0 || k % S2 == 0 ? alternate (Color.green) : Color.green);
					if (k == 0)
						SetMaterial (r, i % S2 == 0 || j % S2 == 0 ? alternate (Color.blue) : Color.blue);
	
					
					t.position = new Vector3 (i, j, k) * gridScale;
					t.localScale = Vector3.one * cubeScale;
					t.parent = cubeParent.transform;
				}		
	}

	void Update ()
	{
		if (Input.GetKeyDown (KeyCode.G)) {
			cubeParent.SetActive (!cubeParent.activeSelf);
		}		
	}

	public void Toggle (bool show)
	{
		cubeParent.SetActive (show);
	}

	static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material> ();

	void SetMaterial (Renderer r, Color c)
	{
		Material m;
		if (!materials.TryGetValue (c, out m)) {
			m = Instantiate (r.material);
			m.color = c;
			materials [c] = m;
		}
		r.sharedMaterial = m;
	}


}
