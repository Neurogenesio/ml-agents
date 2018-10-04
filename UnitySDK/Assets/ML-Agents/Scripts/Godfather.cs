using System.Collections;
using System.Collections.Generic;
using DataClient;
using UnityEngine;
using MLAgents;

public class Godfather : MonoBehaviour {

    public Academy academy;

	private Client socket;
    // Use this for initialization

	void Awake()
	{
		socket = new Client();
	}
    void Start ()
    {
	    int i = 0;
	    while (i < 10)
	    {
		    var result = academy.EnvironmentInternalStep();
		    foreach (var key in result.Keys)
		    {
			    var values = result[key];
			    foreach (var value in values.Value)
			    {
				    foreach (var visual in value.VisualObservations)
				    {
					    socket.Send(visual);
				    }
			    }
		    }
		    Debug.Log("Step:" + i);
		    i++;
		    
	    }
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
