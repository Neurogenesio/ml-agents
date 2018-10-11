using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using DataClient;
using UnityEngine;
using MLAgents;
using UnityScript.Macros;



public class Godfather : MonoBehaviour {

    public Academy academy;

	private Client socket;
    // Use this for initialization

	void Awake()
	{
		try
		{
			socket = new Client();
		}
		catch
		{
			Debug.Log("catch exception");
			//goto Start;
		}
	}
    void Start ()
    {
        /*Start:
			try
			{
				socket = new Client();
			}
	    catch
	    {
		    Debug.Log("catch exception");
		    //goto Start;
	    }

	    while (true)
	    {
		    var result = academy.EnvironmentInternalStep();
		    foreach (var key in result.Keys)
		    {
			    var values = result[key];
			    foreach (var value in values.Value)
			    {
				    foreach (var visual in value.VisualObservations)
				    {
					    try
					    {
						    //byte[] length = BitConverter.GetBytes((visual.Length));
						    socket.Send(visual);
					    }
					    catch
					    {
						    goto Start;
					    }
				    }
			    }
		    }
	    }*/
	    /*int i = 0;
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
		    
	    }*/
	    
	}
	
	// Update is called once per frame
	void FixedUpdate () {	
			var result = academy.EnvironmentInternalStep();
			foreach (var key in result.Keys)
			{
				var values = result[key];
				foreach (var value in values.Value)
				{
					foreach (var visual in value.visualObservationsStruct)
					{
						try
						{
							//byte[] length = BitConverter.GetBytes((visual.Length));
							socket.Send(visual);
						}
						catch
						{
							//goto Start;
						}
					}
				}
			}
		}
	
}
