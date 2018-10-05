using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class chair : MonoBehaviour {

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        System.Random rnd = new System.Random();

        float smooth = 5.0f;
        int rotation = rnd.Next(-120, 120);

        Quaternion rotation_traget = Quaternion.Euler(0, rotation, 0);

        transform.rotation = Quaternion.Slerp(transform.rotation, rotation_traget, Time.deltaTime * smooth);
    }
}
